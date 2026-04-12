using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay; // 최신 버전의 AllocationUtils를 쓰기 위해 필수
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class MatchmakingManager : MonoBehaviour
{
    private Lobby currentLobby;

    async void Start()
    {
        // 1. 유니티 클라우드 서비스 초기화
        await UnityServices.InitializeAsync();
        
        // 2. 익명 로그인 (네트워크 접속을 위한 필수 신분증)
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"로그인 성공! 내 ID: {AuthenticationService.Instance.PlayerId}");
        }
    }

    // 이 함수를 UI의 [매칭 시작] 버튼 OnClick에 연결하세요.
    public async void FindMatch()
    {
        Debug.Log("매칭을 시도합니다...");

        try
        {
            // 1순위: 남이 만들어둔 빈 방이 있는지 찾아서 접속 시도 (수비자 역할)
            await QuickJoinLobby();
        }
        catch (LobbyServiceException e)
        {
            // 2순위: 빈 방이 없어서 에러가 나면, 내가 직접 방을 만듦 (방장/공격자 역할)
            Debug.Log("참가할 방이 없습니다. 새로운 방을 개설합니다.");
            await CreateLobbyAndStartHost();
        }
    }

    // --- 수비자(Client) 로직 ---
    private async Task QuickJoinLobby()
    {
        // 빈 방 퀵 조인
        Lobby lobby = await LobbyService.Instance.QuickJoinLobbyAsync();
        
        // 로비 데이터에서 방장이 숨겨둔 '릴레이 접속 코드'를 빼냄
        string joinCode = lobby.Data["JoinCode"].Value;
        Debug.Log($"방 찾기 성공! 릴레이 코드 [{joinCode}] 로 접속을 시도합니다.");

        // 릴레이 서버에 코드를 내밀고 접속 권한(Allocation) 획득
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        
        // 🚨 [최신 버전 업데이트] AllocationUtils를 사용하여 데이터 변환
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
        
        // NetworkManager의 UTP 트럭에 릴레이 정보 싣기
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        // 클라이언트로 게임 접속!
        NetworkManager.Singleton.StartClient();
    }

    // --- 공격자/방장(Host) 로직 ---
    private async Task CreateLobbyAndStartHost()
    {
        // 나 제외 1명(총 2명)이 들어올 수 있는 릴레이 전용선(Allocation) 뚫기
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
        
        // 다른 사람이 이 전용선에 들어올 수 있게 해주는 '비밀번호(JoinCode)' 발급
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        // 로비(게시판)에 방을 올릴 때, 방금 받은 비밀번호를 함께 적어서 올림
        CreateLobbyOptions options = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new System.Collections.Generic.Dictionary<string, DataObject>
            {
                { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
            }
        };

        // 최대 2명짜리 로비 생성
        currentLobby = await LobbyService.Instance.CreateLobbyAsync("1v1 Magic Match", 2, options);
        Debug.Log($"방 생성 성공! 다른 플레이어를 기다립니다... 릴레이 코드: [{joinCode}]");

        // 🚨 [최신 버전 업데이트] AllocationUtils를 사용하여 데이터 변환
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        
        // NetworkManager의 UTP 트럭에 내 릴레이 정보 싣기
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        // 호스트(서버+클라이언트)로 게임 열기!
        NetworkManager.Singleton.StartHost();
    }
}