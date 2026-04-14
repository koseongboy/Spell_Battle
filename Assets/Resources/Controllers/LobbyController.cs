using UnityEngine;
using Models.RelayMatchmakingService;
using Views.LobbyView;

namespace Controllers.LobbyController
{
    public class LobbyController : MonoBehaviour
    {
        // 🚨 Inspector 노출([SerializeField])을 지우고, 직접 인스턴스를 생성합니다.
        private RelayMatchmakingService matchmakingService; 
        
        [SerializeField] private LobbyView lobbyView;

        private void Awake()
        {
            // Controller가 태어날 때, Service(Model) 객체를 메모리에 생성합니다.
            matchmakingService = new RelayMatchmakingService();
        }

        private async void Start()
        {
            lobbyView.ShowMainMenu();
            lobbyView.UpdateStatus("Initiallizing...");

            // 순수 C# 클래스인 Service의 함수를 호출합니다.
            await matchmakingService.InitializeAndSignInAsync();
            lobbyView.UpdateStatus("Ready");

            lobbyView.randomMatchButton.onClick.AddListener(OnRandomMatchRequest);
            lobbyView.createRoomButton.onClick.AddListener(OnCreateRoomRequest);
            lobbyView.joinRoomButton.onClick.AddListener(OnJoinRoomRequest);
            lobbyView.cancelButton.onClick.AddListener(OnCancelRequest);
        }

        private async void OnRandomMatchRequest()
        {
            lobbyView.UpdateStatus("Finding Random Room...");
            
            var (isHost, joinCode) = await matchmakingService.QuickMatchAsync();

            if (!string.IsNullOrEmpty(joinCode))
            {
                if (isHost)
                {
                    lobbyView.UpdateStatus("New Room Created");
                    lobbyView.ShowRoomInfo(joinCode, true); // 랜덤 매치 대기 화면
                }
                else
                {
                    lobbyView.UpdateStatus("Enter Existing Room");
                    // 클라이언트는 대기 화면 없이 바로 인게임으로 넘어가면 됨
                    lobbyView.gameObject.SetActive(false); 
                }
            }
            else
            {
                lobbyView.UpdateStatus("매칭 중 오류가 발생했습니다.");
            }
        }

        private async void OnCreateRoomRequest()
        {
            lobbyView.UpdateStatus("Creating room...");
            string code = await matchmakingService.CreateRelayRoomAsync();

            if (!string.IsNullOrEmpty(code))
            {
                lobbyView.ShowRoomInfo(code);
                lobbyView.UpdateStatus("You are host now");
            }
            else
            {
                lobbyView.UpdateStatus("error to create room");
            }
        }

        private async void OnJoinRoomRequest()
        {
            string code = lobbyView.GetInputCode();
            if (string.IsNullOrEmpty(code) || code.Length < 6)
            {
                lobbyView.UpdateStatus("wrong code");
                return;
            }

            lobbyView.UpdateStatus("try to enter room...");
            bool isSuccess = await matchmakingService.JoinRelayRoomAsync(code);

            if (isSuccess)
            {
                lobbyView.UpdateStatus("success");
            }
            else
            {
                lobbyView.UpdateStatus("faild. check your code");
            }
        }

        private async void OnCancelRequest()
        {
            lobbyView.UpdateStatus("Canceling...");
            
            // 1. 서버/로비 정리 작업 실행
            await matchmakingService.LeaveLobbyAsync();
            
            // 2. UI 전환
            lobbyView.ShowMainMenu();
            lobbyView.UpdateStatus("Ready");
        }
    }
}
