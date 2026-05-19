using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using System.Threading;

namespace Models.RelayMatchmakingService
{
    public class RelayMatchmakingService
    {
        private Lobby currentLobby;
        private LobbyEventCallbacks lobbyEvents;
        private CancellationTokenSource heartbeatTokenSource;
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string CurrentLobbyCode => currentLobby?.LobbyCode;

        public async Task InitializeAndSignInAsync()
        {
            try
            {
                InitializationOptions options = new InitializationOptions();

#if UNITY_EDITOR
                // 🌟 핵심: 고정된 이름 대신, 실행할 때마다 무작위 난수(GUID)로 프로필을 강제 생성합니다.
                string profileName = System.Guid.NewGuid().ToString().Substring(0, 8);
                options.SetProfile(profileName);
                Debug.Log($"[UGS 초기화] 에디터 테스트 강제 분리 프로필: {profileName}");
#endif

                // 1. 설정한 무작위 프로필로 UGS 초기화
                await UnityServices.InitializeAsync(options);

                // 2. 만약 찌꺼기가 남아 로그인되어 있다고 착각하면 강제로 로그아웃 (초기화)
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut();
                    AuthenticationService.Instance.ClearSessionToken();
                }

                // 3. 완전한 새 유저로 익명 로그인
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                
                // 🌟 본체와 클론의 이 PlayerId 값이 무조건 '다르게' 찍혀야 정상입니다!
                Debug.Log($"[UGS 로그인] 완료! 내 고유 ID: {AuthenticationService.Instance.PlayerId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UGS 에러] 초기화 또는 로그인 실패: {e.Message}");
            }
        }

        // ==========================================
        // 🎲 [기존 기능] 랜덤 빠른 매치 (게시판 이용)
        // ==========================================
        public async Task<(bool isHost, string joinCode)> QuickMatchAsync()
        {
            try
            {
                currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(); 
                string joinCode = currentLobby.Data["JoinCode"].Value;
                
                await JoinRelayRoomAsync(joinCode);
                return (false, joinCode);
            }
            catch (LobbyServiceException)
            {
                string joinCode = await CreateRelayRoomAsync();

                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                    }
                };
                
                currentLobby = await LobbyService.Instance.CreateLobbyAsync("Random Match Room", 2, options);
                
                return (true, joinCode);
            }
        }

        // ==========================================
        // 🔎 공개 방 리스트 검색 (필터 적용)
        // ==========================================
        public async Task<List<Lobby>> GetPublicLobbyListAsync()
        {
            try
            {
                QueryLobbiesOptions options = new QueryLobbiesOptions
                {
                    Count = 25, // 최대 25개까지만 가져오기
                    Filters = new List<QueryFilter>
                    {
                        // 조건 1: 빈자리가 0개보다 많은(GT, Greater Than) 방
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                        // 조건 2: 잠기지 않은 방 (IsLocked가 0인 방)
                        new QueryFilter(QueryFilter.FieldOptions.IsLocked, "0", QueryFilter.OpOptions.EQ)
                    }
                };

                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
                
                Debug.Log($"[리스트 검색] 서버에서 가져온 방 개수: {response.Results.Count}개");
                return response.Results;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"방 검색 실패: {e.Message}");
                return new List<Lobby>(); // 에러 시 빈 리스트 반환
            }
        }

        // ==========================================
        // ✨ [새로 추가된 기능] 2. 방 제목 지정 & 공개/비공개 방 생성
        // ==========================================
        public async Task<string> CreateCustomLobbyAsync(string roomName, bool isPrivate)
        {
            try
            {
                // 1. 진짜 통신을 담당할 릴레이 서버 코드를 먼저 발급받음
                string relayJoinCode = await CreateRelayRoomAsync();
                if (string.IsNullOrEmpty(relayJoinCode)) return null;

                // 2. 가상의 게시판(로비) 설정
                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = isPrivate, // true면 리스트 검색(GetPublicLobbyListAsync)에 안 잡힘
                    Data = new Dictionary<string, DataObject>
                    {
                        // 릴레이 코드는 'Member(방 참가자)'만 볼 수 있도록 숨김 처리
                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                };

                // 3. 로비 서버에 방 등록 (최대 인원 2명)
                currentLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, 2, options);
                StartHeartbeat();
                
                Debug.Log($"커스텀 방 생성 완료! 제목: {roomName}, 로비코드: {currentLobby.LobbyCode}");
                
                // 생성한 방의 로비 접속 코드 반환 (비공개 방일 경우 친구에게 알려줄 코드)
                return currentLobby.LobbyCode;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"커스텀 방 생성 실패: {e.Message}");
                return null;
            }
        }

        // ==========================================
        // ✨ [새로 추가된 기능] 3. 비공개 방 코드로 참여
        // ==========================================
        public async Task<string> JoinCustomLobbyByCodeAsync(string lobbyCode)
        {
            try
            {
                // 1. 유저가 입력한 코드로 로비 입장
                currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
                
                // 2. 방 데이터에서 숨겨진 릴레이 코드를 추출하여 릴레이 접속
                string relayJoinCode = currentLobby.Data["JoinCode"].Value;
                bool isSuccess = await JoinRelayRoomAsync(relayJoinCode);
                return isSuccess ? currentLobby.Name : null;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"비공개 방 코드 접속 실패: {e.Message}");
                return null;
            }
        }

        // ==========================================
        // ✨ [새로 추가된 기능] 4. 공개 방 리스트에서 클릭하여 참여
        // ==========================================
        public async Task<string> JoinCustomLobbyByIdAsync(string lobbyId)
        {
            try
            {
                // 1. 리스트에서 선택한 방의 ID로 로비 입장
                currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
                
                // 2. 방 데이터에서 숨겨진 릴레이 코드를 추출하여 릴레이 접속
                string relayJoinCode = currentLobby.Data["JoinCode"].Value;
                bool isSuccess = await JoinRelayRoomAsync(relayJoinCode);
                return isSuccess ? currentLobby.Name : null;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"공개 방 리스트 접속 실패: {e.Message}");
                return null;
            }
        }

        // ==========================================
        // ⚙️ [기존 기능] 릴레이 통신 기반 로직 및 정리
        // ==========================================
        public async Task<string> CreateRelayRoomAsync()
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                NetworkManager.Singleton.StartHost();

                return joinCode;
            }
            catch (RelayServiceException e) { Debug.LogError(e); return null; }
        }

        public async Task<bool> JoinRelayRoomAsync(string joinCode)
        {
            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");

                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                return NetworkManager.Singleton.StartClient();
            }
            catch (RelayServiceException e) { Debug.LogError(e); return false; }
        }

        public async Task LeaveLobbyAsync()
        {
            if (heartbeatTokenSource != null)
            {
                heartbeatTokenSource.Cancel();
                heartbeatTokenSource.Dispose();
                heartbeatTokenSource = null;
            }
            try
            {
                if (currentLobby != null)
                {
                    if (currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                    {
                        await LockLobbyAsync();
                        await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                    }
                    else
                    {
                        await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
                    }
                    currentLobby = null; 
                }

                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.Shutdown();

                    while (NetworkManager.Singleton.ShutdownInProgress)
                    {
                        await Task.Yield(); 
                    }
                    
                    Debug.Log("네트워크 매니저가 완전히 종료되고 포트가 정리되었습니다.");
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"로비 퇴장 중 오류: {e.Message}");
            }
        }

        public async Task LockLobbyAsync()
        {
            if (currentLobby != null)
            {
                try
                {
                    UpdateLobbyOptions options = new UpdateLobbyOptions
                    {
                        IsLocked = true 
                    };
                    currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, options);
                    Debug.Log("방이 잠겼습니다. 더 이상 새로운 유저가 매칭되지 않습니다.");
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError($"방 잠금 실패: {e.Message}");
                }
            }
        }

        public async Task SubscribeToLobbyEvents()
        {
            if (currentLobby == null) return;

            lobbyEvents = new LobbyEventCallbacks();
            // 로비 정보가 변경되었을 때 발동할 이벤트 연결
            lobbyEvents.LobbyChanged += OnLobbyChanged;

            try
            {
                await LobbyService.Instance.SubscribeToLobbyEventsAsync(currentLobby.Id, lobbyEvents);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"로비 이벤트 구독 실패: {e.Message}");
            }
        }

        private void OnLobbyChanged(ILobbyChanges changes)
        {
            // 상대방이 들어와서 로비의 Player 목록에 변화가 생겼는지 체크
            if (changes.PlayerJoined.Changed && changes.PlayerJoined.Value.Count > 0)
            {
                Debug.Log("웹 서버: 새로운 플레이어가 로비 슬롯에 들어왔습니다!");
                // TODO: 컨트롤러에 이벤트를 쏴서 lobbyView.SetLoadingPanel(true) 실행
            }
        }

        // ==========================================
        // 💓 15초마다 서버에 생존 신고 보내기
        // ==========================================
        private async void StartHeartbeat()
        {
            // 1. 혹시나 이전에 가동 중이던 취소선이 남아있다면 확실하게 끄고 메모리 정리
            if (heartbeatTokenSource != null)
            {
                heartbeatTokenSource.Cancel();
                heartbeatTokenSource.Dispose();
            }
            // 2. 이번 루프에서 사용할 고유한 취소선 발급
            heartbeatTokenSource = new CancellationTokenSource();
            CancellationToken token = heartbeatTokenSource.Token;
            Debug.Log("💓 [로비 하트비트] 루프 시작");
            try
            {
                // 토큰에 취소 요청이 들어오기 전까지 무한 반복
                while (currentLobby != null && !token.IsCancellationRequested)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
                    Debug.Log("💓 [로비 하트비트] 서버에 생존 신고 완료");

                    // 🌟 핵심: Task.Delay에도 토큰을 매개변수로 넘겨줍니다.
                    // 이렇게 하면 15초 동안 잠자는 도중에 취소 신호가 와도 즉시 깨어나 루프를 종료합니다.
                    await Task.Delay(15000, token); 
                }
            }
            catch (TaskCanceledException)
            {
                // token.Cancel()이 호출되면 이쪽 예외로 떨어집니다. (안전하고 정상적인 종료 상태)
                Debug.Log("💓 [로비 하트비트] 취소 신호를 받아 루프가 안전하게 종료되었습니다.");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"하트비트 통신 실패: {e.Message}");
            }
        }
    }
}