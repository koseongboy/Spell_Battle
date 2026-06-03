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
using DefaultNamespace;

namespace Models.RelayMatchmakingService {
    public class RelayMatchmakingService {
// 1. 싱글톤 인스턴스 선언
        private static RelayMatchmakingService instance;
        public static RelayMatchmakingService Instance => instance ??= new RelayMatchmakingService();

        // 외부에서 new 키워드로 인스턴스를 중복 생성하지 못하도록 생성자를 private으로 제한
        private RelayMatchmakingService() { }

        private Lobby currentLobby;
        private LobbyEventCallbacks lobbyEvents;
        private CancellationTokenSource heartbeatTokenSource;

        // 비동기 중복 요청 방지용 락(Lock) 플래그
        private bool isProcessing = false;

        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string CurrentLobbyCode => currentLobby?.LobbyCode;

        public string CurrentLobbyName => currentLobby?.Name;

        // Players 리스트 안에, ID가 현재 로비의 HostId와 다른 플레이어가 존재하는지 검사
        public bool HasGuest => currentLobby != null &&
                                currentLobby.Players.Exists(player => player.Id != currentLobby.HostId);

        public async Task InitializeAndSignInAsync() {
            try {
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
                if (AuthenticationService.Instance.IsSignedIn) {
                    AuthenticationService.Instance.SignOut();
                    AuthenticationService.Instance.ClearSessionToken();
                }

                // 3. 완전한 새 유저로 익명 로그인
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                // 🌟 본체와 클론의 이 PlayerId 값이 무조건 '다르게' 찍혀야 정상입니다!
                Debug.Log($"[UGS 로그인] 완료! 내 고유 ID: {AuthenticationService.Instance.PlayerId}");
            }
            catch (System.Exception e) {
                Debug.LogError($"[UGS 에러] 초기화 또는 로그인 실패: {e.Message}");
            }
        }

        // ==========================================
        // 🎲 [기존 기능] 랜덤 빠른 매치 (게시판 이용)
        // ==========================================
        public async Task<(bool isHost, string joinCode)> QuickMatchAsync() {
            if (isProcessing) {
                Debug.LogWarning("이미 네트워크 통신이 진행 중입니다.");
                return (false, null);
            }

            try {
                isProcessing = true; // 락 설정

                
                currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
                string joinCode = currentLobby.Data["JoinCode"].Value;

                await JoinRelayRoomAsync(joinCode);
                return (false, joinCode);
            }
            catch (LobbyServiceException) {
                string joinCode = await CreateRelayRoomAsync();

                string hostName = "Guest";
                int hostLevel = 1;
                if (Managers.LocalDataManagers.LocalDataManager.Instance != null)
                {
                    hostName = Managers.LocalDataManagers.LocalDataManager.Instance.nickname; //
                    hostLevel = Managers.LocalDataManagers.LocalDataManager.Instance.level;  //
                }


                CreateLobbyOptions options = new CreateLobbyOptions {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject> {
                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) },
                        
                        // 🌟 커스텀 방 생성과 동일한 키값("HostName", "HostLevel")으로 등록!
                        { "HostName", new DataObject(DataObject.VisibilityOptions.Public, hostName, DataObject.IndexOptions.S1) }, //
                        { "HostLevel", new DataObject(DataObject.VisibilityOptions.Public, hostLevel.ToString(), DataObject.IndexOptions.N1) } //
                    }
                };

                currentLobby = await LobbyService.Instance.CreateLobbyAsync("Random Match Room", 2, options);

                return (true, joinCode);
            }
            finally {
                isProcessing = false; // 통신 완료 후 락 해제
            }
        }

        // ==========================================
        // 🔎 공개 방 리스트 검색 (필터 적용)
        // ==========================================
        public async Task<List<Lobby>> GetPublicLobbyListAsync() {
            if (!IsSignedIn) {
                Debug.LogWarning("[Matchmaking] UGS 서비스가 로그인되어 있지 않습니다.");
                return new List<Lobby>();
            }

            try {
                // 원래 사용하시던 완벽한 필터 코드 복구!
                QueryLobbiesOptions options = new QueryLobbiesOptions {
                    Count = 25,
                    Filters = new List<QueryFilter> {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "1", QueryFilter.OpOptions.EQ),
                        new QueryFilter(QueryFilter.FieldOptions.IsLocked, "0", QueryFilter.OpOptions.EQ) // "0"도 정상 작동하는 것이 맞습니다.
                    }
                };

                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

                Debug.Log($"[리스트 검색 성공!] 서버에서 가져온 방 개수: {response.Results.Count}개");
                return response.Results;
            }
            catch (System.Exception e) {
                // 🌟 핵심 변경: LogError를 LogWarning으로 바꿉니다.
                // 유니티 SDK가 간헐적으로 헛발질을 하더라도, 게임이 멈추지(Error Pause) 않고 자연스럽게 다음 동작(또는 재검색)을 할 수 있게 됩니다.
                Debug.LogWarning($"[Matchmaking] 방 검색 실패 (SDK 내부 딜레이, 무시 가능): {e.Message}");
                return new List<Lobby>();
            }
        }

        // ==========================================
        // ✨ [새로 추가된 기능] 2. 방 제목 지정 & 공개/비공개 방 생성
        // ==========================================
        public async Task<string> CreateCustomLobbyAsync(string roomName, bool isPrivate, string hostName = "",
            int hostLevel = 0) {
            try {
                // 1. 진짜 통신을 담당할 릴레이 서버 코드를 먼저 발급받음
                string relayJoinCode = await CreateRelayRoomAsync();
                if (string.IsNullOrEmpty(relayJoinCode)) return null;
                //hostname을 입력 안했다면 LDM에서 가져옴
                if (string.IsNullOrEmpty(hostName) && Managers.LocalDataManagers.LocalDataManager.Instance != null)
                {
                    hostName = Managers.LocalDataManagers.LocalDataManager.Instance.nickname; //
                    hostLevel = Managers.LocalDataManagers.LocalDataManager.Instance.level;  //
                    Debug.Log($"[Matchmaking] 📦 로컬 데이터 매니저로부터 닉네임({hostName})을 자동으로 갱신했습니다.");
                }

                // 2. 가상의 게시판(로비) 설정
                CreateLobbyOptions options = new CreateLobbyOptions {
                    IsPrivate = isPrivate, // true면 리스트 검색(GetPublicLobbyListAsync)에 안 잡힘
                    Data = new Dictionary<string, DataObject> {
                        // 릴레이 코드는 'Member(방 참가자)'만 볼 수 있도록 숨김 처리
                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },

                        // 방장 이름 (방에 입장하지 않은 사람도 리스트에서 봐야 하므로 Public 설정)
                        // 추후 방장 이름으로 검색할 수 있도록 IndexOptions.S1 할당 (String 인덱스)
                        {
                            "HostName",
                            new DataObject(DataObject.VisibilityOptions.Public, hostName, DataObject.IndexOptions.S1)
                        },

                        // 방장 레벨 (숫자도 문자열로 변환하여 저장, 추후 레벨 제한 필터링을 위해 IndexOptions.N1 할당)
                        {
                            "HostLevel",
                            new DataObject(DataObject.VisibilityOptions.Public, hostLevel.ToString(),
                                DataObject.IndexOptions.N1)
                        }
                    }
                };

                // 3. 로비 서버에 방 등록 (최대 인원 2명)
                currentLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, 2, options);
                StartHeartbeat(); // (작성하신 Heartbeat 코루틴/메서드 호출)

                Debug.Log($"커스텀 방 생성 완료! 제목: {roomName}, 로비코드: {currentLobby.LobbyCode}");

                // 생성한 방의 로비 접속 코드 반환
                return currentLobby.LobbyCode;
            }
            catch (LobbyServiceException e) {
                Debug.LogError($"커스텀 방 생성 실패: {e.Message}");
                return null;
            }
        }

        // ==========================================
        // ✨ [새로 추가된 기능] 3. 비공개 방 코드로 참여
        // ==========================================
        public async Task<string> JoinCustomLobbyByCodeAsync(string lobbyCode) {
            try {
                // 1. 유저가 입력한 코드로 로비 입장
                currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

                // 2. 방 데이터에서 숨겨진 릴레이 코드를 추출하여 릴레이 접속
                string relayJoinCode = currentLobby.Data["JoinCode"].Value;
                bool isSuccess = await JoinRelayRoomAsync(relayJoinCode);
                return isSuccess ? currentLobby.Name : null;
            }
            catch (LobbyServiceException e) {
                Debug.LogError($"비공개 방 코드 접속 실패: {e.Message}");
                return null;
            }
        }

        // ==========================================
        // ✨ [새로 추가된 기능] 4. 공개 방 리스트에서 클릭하여 참여
        // ==========================================
        public async Task<string> JoinCustomLobbyByIdAsync(string lobbyId) {
            try {
                // 1. 리스트에서 선택한 방의 ID로 로비 입장
                currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

                // 2. 방 데이터에서 숨겨진 릴레이 코드를 추출하여 릴레이 접속
                string relayJoinCode = currentLobby.Data["JoinCode"].Value;
                bool isSuccess = await JoinRelayRoomAsync(relayJoinCode);
                return isSuccess ? currentLobby.Name : null;
            }
            catch (LobbyServiceException e) {
                Debug.LogError($"공개 방 리스트 접속 실패: {e.Message}");
                return null;
            }
        }

        // ==========================================
        // ⚙️ [기존 기능] 릴레이 통신 기반 로직 및 정리
        // ==========================================
        public async Task<string> CreateRelayRoomAsync() {
            try {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                NetworkManager.Singleton.StartHost();

                return joinCode;
            }
            catch (RelayServiceException e) {
                Debug.LogError(e);
                return null;
            }
        }

        public async Task<bool> JoinRelayRoomAsync(string joinCode) {
            try {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");

                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                return NetworkManager.Singleton.StartClient();
            }
            catch (RelayServiceException e) {
                Debug.LogError(e);
                return false;
            }
        }

        public async Task LeaveLobbyAsync() {
            // 하트비트 안전종료
            if (heartbeatTokenSource != null) {
                heartbeatTokenSource.Cancel();
                heartbeatTokenSource.Dispose();
                heartbeatTokenSource = null;
            }

            try {
                if (currentLobby != null) {
                    if (lobbyEvents != null) {
                        lobbyEvents.LobbyChanged -= OnLobbyChanged;
                        lobbyEvents = null;
                    }

                    if (currentLobby.HostId == AuthenticationService.Instance.PlayerId) {
                        // 팩트: LockLobby를 하면 잠금 처리에 시간이 소요되어 Delete가 씹히거나 지연될 수 있습니다.
                        // 이미 방을 터트릴 것이므로 Lock 과정 없이 즉시 Delete 합니다.
                        await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                        Debug.Log("[Lobby] 방장이 로비를 완전히 삭제했습니다.");
                    }
                    else {
                        await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id,
                            AuthenticationService.Instance.PlayerId);
                        Debug.Log("[Lobby] 게스트가 로비에서 정상 퇴장했습니다.");
                    }
                }
            }
            catch (LobbyServiceException e) {
                // 에러가 났더라도 방 데이터가 이미 만료되었을 확률이 높습니다.
                Debug.LogWarning($"로비 퇴장/삭제 통신 지연 (무시 가능): {e.Message}");
            }
            finally {
                // 팩트: NetworkManager.Shutdown은 여기(Model)서 빼고 Controller에게 맡깁니다.
                currentLobby = null;
                isProcessing = false;
            }
        }

        public async Task LockLobbyAsync() {
            if (currentLobby != null) {
                try {
                    UpdateLobbyOptions options = new UpdateLobbyOptions {
                        IsLocked = true
                    };
                    currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, options);
                    Debug.Log("방이 잠겼습니다. 더 이상 새로운 유저가 매칭되지 않습니다.");
                }
                catch (LobbyServiceException e) {
                    Debug.LogError($"방 잠금 실패: {e.Message}");
                }
            }
        }

        public async Task SubscribeToLobbyEvents() {
            if (currentLobby == null) return;

            lobbyEvents = new LobbyEventCallbacks();
            // 로비 정보가 변경되었을 때 발동할 이벤트 연결
            lobbyEvents.LobbyChanged += OnLobbyChanged;

            try {
                await LobbyService.Instance.SubscribeToLobbyEventsAsync(currentLobby.Id, lobbyEvents);
            }
            catch (LobbyServiceException e) {
                Debug.LogError($"로비 이벤트 구독 실패: {e.Message}");
            }
        }

        private void OnLobbyChanged(ILobbyChanges changes) {
            // 상대방이 들어와서 로비의 Player 목록에 변화가 생겼는지 체크
            if (changes.PlayerJoined.Changed && changes.PlayerJoined.Value.Count > 0) {
                Debug.Log("웹 서버: 새로운 플레이어가 로비 슬롯에 들어왔습니다!");
                // TODO: 컨트롤러에 이벤트를 쏴서 lobbyView.SetLoadingPanel(true) 실행
            }
        }

        // ==========================================
        // 💓 15초마다 서버에 생존 신고 보내기
        // ==========================================
        private async void StartHeartbeat() {
            // 1. 혹시나 이전에 가동 중이던 취소선이 남아있다면 확실하게 끄고 메모리 정리
            if (heartbeatTokenSource != null) {
                heartbeatTokenSource.Cancel();
                heartbeatTokenSource.Dispose();
            }

            // 2. 이번 루프에서 사용할 고유한 취소선 발급
            heartbeatTokenSource = new CancellationTokenSource();
            CancellationToken token = heartbeatTokenSource.Token;
            Debug.Log("💓 [로비 하트비트] 루프 시작");
            try {
                // 토큰에 취소 요청이 들어오기 전까지 무한 반복
                while (currentLobby != null && !token.IsCancellationRequested) {
                    await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
                    Debug.Log("💓 [로비 하트비트] 서버에 생존 신고 완료");

                    // 🌟 핵심: Task.Delay에도 토큰을 매개변수로 넘겨줍니다.
                    // 이렇게 하면 15초 동안 잠자는 도중에 취소 신호가 와도 즉시 깨어나 루프를 종료합니다.
                    await Task.Delay(15000, token);
                }
            }
            catch (TaskCanceledException) {
                // token.Cancel()이 호출되면 이쪽 예외로 떨어집니다. (안전하고 정상적인 종료 상태)
                Debug.Log("💓 [로비 하트비트] 취소 신호를 받아 루프가 안전하게 종료되었습니다.");
            }
            catch (LobbyServiceException e) {
                Debug.LogError($"하트비트 통신 실패: {e.Message}");
            }
        }
    }
}