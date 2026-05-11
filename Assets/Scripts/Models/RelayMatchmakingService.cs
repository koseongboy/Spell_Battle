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

namespace Models.RelayMatchmakingService
{
    public class RelayMatchmakingService
    {
        // ✨ [추가] 현재 세션의 로비 정보를 저장할 변수
        private Lobby currentLobby;

        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        public async Task InitializeAndSignInAsync()
        {
            await UnityServices.InitializeAsync();
            if (!IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        // 랜덤 매치 (게시판 이용)
        public async Task<(bool isHost, string joinCode)> QuickMatchAsync()
        {
            try
            {
                // 1. 빠른 참가 시도
                currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(); // ✨ 정보 저장
                string joinCode = currentLobby.Data["JoinCode"].Value;
                
                await JoinRelayRoomAsync(joinCode);
                return (false, joinCode);
            }
            catch (LobbyServiceException)
            {
                // 2. 방이 없으면 생성
                string joinCode = await CreateRelayRoomAsync();

                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new System.Collections.Generic.Dictionary<string, DataObject>
                    {
                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                    }
                };
                
                // ✨ 내가 만든 방 정보를 저장
                currentLobby = await LobbyService.Instance.CreateLobbyAsync("Random Match Room", 2, options);
                
                return (true, joinCode);
            }
        }

        // 프라이빗 방 생성
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

        // 프라이빗 방 입장
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

        // ✨ [수정된 방 나가기 및 세션 정리 로직]
        public async Task LeaveLobbyAsync()
        {
            try
            {
                await LockLobbyAsync();
                // 1. 로비 서비스 정리 (기존과 동일)
                if (currentLobby != null)
                {
                    if (currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                    {
                        await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                    }
                    else
                    {
                        await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
                    }
                    currentLobby = null; 
                }

                // 2. NGO 연결 종료 및 찌꺼기 청소 대기
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.Shutdown();

                    // 🚨 핵심 해결책: Shutdown은 '비동기'이므로 완전히 꺼질 때까지 프레임을 넘기며 기다려야 합니다.
                    while (NetworkManager.Singleton.ShutdownInProgress)
                    {
                        await Task.Yield(); // 완전히 꺼질 때까지 다음 프레임으로 대기
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
                        IsLocked = true // 게시판에서 방을 비공개/잠금 처리함
                    };
                    // 로비 정보를 서버에 업데이트하고, 내 변수도 최신화
                    currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, options);
                    Debug.Log("방이 잠겼습니다. 더 이상 새로운 유저가 매칭되지 않습니다.");
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError($"방 잠금 실패: {e.Message}");
                }
            }
        }
    }
}
