using UnityEngine;
using Models.RelayMatchmakingService;
using Views.LobbyView;
using Unity.Netcode;
using System.Threading.Tasks;

namespace Controllers.LobbyController
{
    public class LobbyController : MonoBehaviour
    {
        private RelayMatchmakingService matchmakingService;
        [SerializeField] private LobbyView lobbyView;

        // 현재 유저가 대결방에 들어가 있는지 여부를 추적 (뒤로가기 버튼 로직 처리용)
        private bool isInWaitingRoom = false; 

        private void Awake()
        {
            matchmakingService = new RelayMatchmakingService();
        }

        private async void Start()
        {
            lobbyView.UpdateStatus("서버 초기화 중...");
            lobbyView.SetLoadingPanel(true, "서버에 연결중입니다...");
            await matchmakingService.InitializeAndSignInAsync();
            lobbyView.SetLoadingPanel(false);
            
            // 초기 화면 세팅: 로비 화면 + 대결 만들기 탭
            lobbyView.SwitchMainView(MainViewType.Lobby);
            lobbyView.ShowLobbyTab(LobbyTabType.Create);

            // 1. 글로벌 버튼 연결
            lobbyView.globalBackButton.AddAsyncListener(OnGlobalBackRequested);
            // (설정, 친구 버튼은 나중에 기능 추가 시 연결)

            // 2. 탭 이동 버튼 연결
            lobbyView.tabCreateModeButton.onClick.AddListener(() => lobbyView.ShowLobbyTab(LobbyTabType.Create));
            lobbyView.tabSearchModeButton.onClick.AddListener(OnTabSearchRequested);

            // 3. 접속/생성 액션 버튼 연결
            lobbyView.confirmCreateButton.AddAsyncListener(OnConfirmCreateAsync);
            lobbyView.joinRoomByCodeButton.onClick.AddListener(OnConfirmJoinByCode);

            lobbyView.confirmJoinFromListButton.onClick.AddListener(() => OnConfirmJoinFromList(lobbyView.GetSelectedLobbyId()));

        }

        // ==========================================
        // ❌ 오브젝트가 파괴되거나 프로그램이 종료될 때 실행
        // ==========================================
        private void OnDestroy()
        {
            // 게임이 꺼지거나 오브젝트가 사라질 때, 
            // 돌고 있던 코루틴이나 비동기 작업들이 찌꺼기를 남기지 않도록 정리합니다.
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
            }

            // 🌟 핵심: 방을 파놓은 상태에서 씬이 바뀌거나 앱이 꺼지려고 한다면
            if (isInWaitingRoom && matchmakingService != null)
            {
                Debug.Log("LobbyController 파괴 감지: 로비에서 안전하게 퇴장 처리를 시도합니다.");
                
                // OnDestroy 내부에서는 async/await의 완벽한 대기를 보장할 수 없으므로,
                // Fire-and-Forget 형태로 무조건 서버에 '나 나간다'는 패킷을 던지고 프로세스를 종료합니다.
                _ = matchmakingService.LeaveLobbyAsync();
            }
        }

        private void SetupNetworkCallbacks()
        {
            if (NetworkManager.Singleton != null)
            {
                // StartHost나 StartClient가 불리기 '전'에 무조건 미리 세팅!
                NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;

                // 중복 구독을 막기 위해 뺐다가 다시 연결
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        // --- 탭 이동: 방 찾기 ---
        private async void OnTabSearchRequested()
        {
            lobbyView.ShowLobbyTab(LobbyTabType.Search);
            lobbyView.UpdateStatus("방 목록을 불러오는 중...");
            lobbyView.SetLoadingPanel(true, "방 목록을 불러오는 중..."); //todo: 스크롤 뷰 안에서만 따로 로딩을 적용할 지
            var lobbies = await matchmakingService.GetPublicLobbyListAsync();
            lobbyView.SetLoadingPanel(false);
            lobbyView.RenderRoomList(lobbies);
        }

        // --- 액션 1: 커스텀 방 생성 ---
        private async Task OnConfirmCreateAsync()
        {
            string title = lobbyView.GetRoomTitle();
            bool isPrivate = lobbyView.GetIsPrivate();

            if (string.IsNullOrEmpty(title)) { lobbyView.UpdateStatus("방 제목을 입력하세요!"); return; }
            lobbyView.SetLoadingPanel(true, "대결방 만드는 중...");
            try
            {
                SetupNetworkCallbacks();
                lobbyView.UpdateStatus("방 생성 중...");
                string lobbyCode = await matchmakingService.CreateCustomLobbyAsync(title, isPrivate);

                if (!string.IsNullOrEmpty(lobbyCode))
                {
                    lobbyView.UpdateStatus($"방 생성 성공! 대결방으로 이동합니다. (코드: {lobbyCode})");
                    EnterWaitingRoom(title, lobbyCode);
                }
            }
            finally
            {
                lobbyView.SetLoadingPanel(false);
            }
            
        }

        // --- 액션 2: 코드로 비공개 방 참여 ---
        private async void OnConfirmJoinByCode()
        {
            string code = lobbyView.GetInputCode();
            if (string.IsNullOrEmpty(code)) { lobbyView.UpdateStatus("코드를 입력하세요!"); return; }

            lobbyView.SetLoadingPanel(true, "대결방에 접속하고 있습니다...");
            try
            {
                lobbyView.UpdateStatus($"{code} 방에 접속 시도 중...");
                string title = await matchmakingService.JoinCustomLobbyByCodeAsync(code);
                
                if (title != null) EnterWaitingRoom(title, code);
                else lobbyView.UpdateStatus("접속 실패. 코드를 다시 확인하세요.");
            }
            finally
            {
                lobbyView.SetLoadingPanel(false);
            }
        }

        // --- 액션 3: 리스트에서 선택하여 참여 ---
        private async void OnConfirmJoinFromList(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId)) { lobbyView.UpdateStatus("입장할 방을 선택하세요."); return; }

            lobbyView.UpdateStatus("방에 접속 시도 중...");
            lobbyView.SetLoadingPanel(true, "방 접속 중..");
            string title = await matchmakingService.JoinCustomLobbyByIdAsync(lobbyId);
            lobbyView.SetLoadingPanel(false);
            
            if (title != null) EnterWaitingRoom(title, matchmakingService.CurrentLobbyCode);
            else lobbyView.UpdateStatus("접속 실패. 이미 꽉 찼거나 없는 방입니다.");
        }

        // --- 상태 전환: 대결방 진입 ---
        private void EnterWaitingRoom(string title, string joinCode)
        {
            isInWaitingRoom = true;
            lobbyView.SetWaitingRoomTitle(title);
            lobbyView.SetWaitingRoomJoinCode(joinCode);
            lobbyView.SwitchMainView(MainViewType.WaitingRoom);
            // 🌟 1. 일단 손님 자리는 비워둠 ([+] 버튼 켜기)
            lobbyView.SetGuestSlotActive(false);

            // 🌟 2. 네트워크 매니저가 켜져 있다면 실시간 이벤트 구독 시작!
            if (NetworkManager.Singleton != null)
            {

                // 만약 내가 방장이 아니라 '손님' 자격으로 막 들어온 거라면, 내 화면엔 내가 손님이므로 즉시 UI 활성화
                if (!NetworkManager.Singleton.IsHost && NetworkManager.Singleton.IsClient)
                {
                    lobbyView.SetGuestSlotActive(true);
                }
            }
        }

        // 누군가 방에 접속 (Awake와 같다 생각)
        private void OnConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request, 
            NetworkManager.ConnectionApprovalResponse response)
        {
            // 내가 방장(Host)일 때만 처리
            if (NetworkManager.Singleton.IsHost)
            {
                // request.ClientNetworkId 가 0번(방장 본인)이 아니라면 손님이 들어온 것임!
                if (request.ClientNetworkId != NetworkManager.Singleton.LocalClientId)
                {
                    // todo: 미니 UI
                    lobbyView.SetLoadingPanel(true, "상대 플레이어가 입장 중입니다...");
                    
                    if (connectionTimeoutCoroutine != null) StopCoroutine(connectionTimeoutCoroutine);
                    connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutRoutine(request.ClientNetworkId));
                }
            }

            // 2. 접속을 허가해 줍니다. (이 처리를 해야 OnClientConnected로 넘어갑니다)
            response.Approved = true;
            response.CreatePlayerObject = true; 
            response.Pending = false;
        }

        // 누군가 방에 접속 (Start와 같다 생각)
        private void OnClientConnected(ulong clientId)
        {
            // 방장(나) 외에 누군가 들어왔다면 손님 UI 켜기
            if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsList.Count > 1)
            {
                lobbyView.SetGuestSlotActive(true);
                lobbyView.UpdateStatus("상대방이 입장했습니다!");
                lobbyView.SetLoadingPanel(false);
            }
        }

        // 누군가 방에서 나갔을 때
        private async void OnClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                // [방장 시점] 손님이 나간 경우: 다시 [+] 버튼 띄우기
                if (NetworkManager.Singleton.ConnectedClientsList.Count <= 1)
                {
                    lobbyView.SetGuestSlotActive(false);
                    lobbyView.UpdateStatus("상대방이 퇴장했습니다.");
                }
            }
            else
            {
                // [손님 시점] 서버(방장)와의 연결이 끊어진 경우
                // (방장이 Shutdown을 하면 손님의 LocalClientId로 연결 끊김 콜백이 들어옵니다)
                if (clientId == NetworkManager.Singleton.LocalClientId || clientId == 0)
                {
                    lobbyView.UpdateStatus("방장이 방을 해산했습니다. 메인 로비로 돌아갑니다.");
                    lobbyView.SetLoadingPanel(true, "방장이 접속을 종료하여 메인 화면으로 돌아갑니다.");
                    await ReturnToLobbyMain(true); // 방 폭파 알림과 함께 강제 퇴장 처리
                    lobbyView.SetLoadingPanel(false);
                }
            }
        }

        // --- 액션 4: 글로벌 뒤로가기 버튼 ---
        private async Task OnGlobalBackRequested()
        {
            if (isInWaitingRoom)
            {
                // 유저가 직접 [뒤로] 버튼을 눌러서 나가는 경우
                lobbyView.UpdateStatus("내가 방에서 나갑니다...");
                lobbyView.SetLoadingPanel(true, "메인으로 돌아갑니다...");
                await ReturnToLobbyMain(); 
                lobbyView.SetLoadingPanel(false);
            }
            else
            {
                lobbyView.UpdateStatus("메인 타이틀 화면으로 돌아갑니다.");
                lobbyView.SwitchMainView(MainViewType.Lobby);
                lobbyView.ShowLobbyTab(LobbyTabType.Create);
            }
            
        }

        private async Task ReturnToLobbyMain(bool isForce = false)
        {
            if (!isInWaitingRoom) return;

            // 1. 이벤트 구독 해제 (중복 호출 방지)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if(!isForce)
            {
                // 2. 서버 통신 및 네트워크 매니저 끄기 (LeaveLobbyAsync 내부에서 알아서 Host/Client 구분해 처리함)
                await matchmakingService.LeaveLobbyAsync();
            }
            // 3. 화면을 다시 로비 탭으로 스위칭
            isInWaitingRoom = false;
            lobbyView.SwitchMainView(MainViewType.Lobby);
            lobbyView.ShowLobbyTab(LobbyTabType.Create);
        }

        // 현재 실행 중인 타이머를 기억할 변수
        private Coroutine connectionTimeoutCoroutine;

        // ==========================================
        // ⏱️ 5초 타임아웃 코루틴
        // ==========================================
        private System.Collections.IEnumerator ConnectionTimeoutRoutine(ulong clientId)
        {
            // 🌟 정확히 5초를 기다립니다.
            yield return new WaitForSeconds(5f);

            // --- 5초가 지났는데도 이 코드가 실행된다면? (연결 실패/지연) ---
            
            Debug.LogWarning("상대방의 연결이 너무 오래 걸려 취소되었습니다.");
            
            // 1. 무한 로딩창 끄기
            lobbyView.SetLoadingPanel(false);
            lobbyView.UpdateStatus("상대방의 연결 상태가 불안정하여 취소되었습니다.");

            // 2. 혹시라도 비정상적으로 남아있을 손님 연결 강제 끊기
            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
                lobbyView.SetGuestSlotActive(false); // 다시 + 버튼 띄우기
            }
        }
    }
}