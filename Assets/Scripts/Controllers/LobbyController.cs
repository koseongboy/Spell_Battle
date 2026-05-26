using System.Text.RegularExpressions;
using UnityEngine;
using Models.RelayMatchmakingService;
using Views.LobbyView;
using Unity.Netcode;
using System.Threading.Tasks;
using DefaultNamespace;

namespace Controllers.LobbyController
{
    public class LobbyController : MonoBehaviour
    {
        public static LobbyController Instance { get; private set; }

        private RelayMatchmakingService matchmakingService;
        [SerializeField] private LobbyView lobbyView;

        private Lobby_FullScreen ui_Lobby;
        private EnterGame_FullScreen ui_EnterGame;

        // 현재 유저가 대결방에 들어가 있는지 여부를 추적 (뒤로가기 버튼 로직 처리용)
        private bool isInWaitingRoom = false; 

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            matchmakingService = new RelayMatchmakingService();
        }

        private async void Start()
        {
            CommonUIController.Instance.ShowLoading();
            
            // 서버 로그인 진행
            await matchmakingService.InitializeAndSignInAsync();

            CommonUIController.Instance.DoneLoading();
            // Lobby UI 불러오기
            CommonUIController.Instance.ChangeFullScreen("Lobby_FullScreen");
        }

        public void RegisterLobbyUI(Lobby_FullScreen ui) {
            ui_Lobby = ui;
        }

        public void RegisterEnterGameUI(EnterGame_FullScreen ui) {
            ui_EnterGame = ui;
        }
        public void UnregisterEnterGameUI() {
            ui_EnterGame = null;
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

        // '게임 시작'
        public void OnGameStartPressed() {
            Debug.Log("[LobbyController] OnGameStartPressed");
            
            if (ui_Lobby == null) {
                Debug.Log("[LobbyController] 잘못된 호출입니다. ui_Lobby null입니다.");
                return;
            }
            
            CommonUIController.Instance.ChangeFullScreen("EnterGame_FullScreen");
        }

        // '덱'
        public void OnDeckPressed() {
            Debug.Log("[LobbyController] OnDeckPressed");
        }

        // '튜토리얼'
        public void OnTutorialPressed() {
            Debug.Log("[LobbyController] OnTutorialPressed");
        }

        // '크레딧'
        public void OnCreditPressed() {
            Debug.Log("[LobbyController] OnCreditPressed");
            CommonUIController.Instance.ShowBlackAlert("헤헷 미구현입니다. 그치만 저희 열심히 만들었어요.");
        }


        public void GoBackToLobby() {
            // TODO : '로비로 돌아가시겠습니까?' confirm
            
        }
        

        // --- 탭 이동: 방 찾기 ---
        public async void OnFindRoomPressed() 
        {
            if (lobbyView == null) return;
            
            CommonUIController.Instance.ShowLoading();
            
            var lobbies = await matchmakingService.GetPublicLobbyListAsync();
            
            CommonUIController.Instance.DoneLoading();
            ui_EnterGame.UpdateUI_RoomList( lobbies );
        }
        
        // --- 액션 1: 커스텀 방 생성 ---
        public async Task OnConfirmCreateAsync()
        {
            if (ui_EnterGame == null) return;
            
            string rawTitle = ui_EnterGame.GetInput_RoomName();
            bool isPrivate = ui_EnterGame.GetInput_IsRoomPublic();
            if (string.IsNullOrEmpty(rawTitle)) { CommonUIController.Instance.ShowRedAlert("방 제목을 입력하세요!"); return; }
            string title = FilterTitle( rawTitle ); // 필터링
            
            CommonUIController.Instance.ShowLoading();
            try
            {
                SetupNetworkCallbacks();
                string lobbyCode = await matchmakingService.CreateCustomLobbyAsync(title, isPrivate);

                if (!string.IsNullOrEmpty(lobbyCode))
                {
                    CommonUIController.Instance.DoneLoading();
                    EnterWaitingRoom(title, lobbyCode);
                }
            }
            finally
            {
                CommonUIController.Instance.DoneLoading();
            }
        }

        private string FilterTitle(string inputTxt) {
            // 1. 앞뒤 공백 제거 (Trim)
            inputTxt = inputTxt.Trim();

            // 2. TMP Rich Text 태그 제거 (정규식: < 로 시작해서 > 로 끝나는 모든 문자열 제거)
            inputTxt = Regex.Replace(inputTxt, "<.*?>", string.Empty);

            // 3. 금칙어 필터링 (마스킹 처리 또는 차단)
            // TODO : 욕설 필터링

            // 4. 허용되지 않은 특수문자 제거
            inputTxt = Regex.Replace(inputTxt, @"[^a-zA-Z0-9가-힣\s]", "");

            return inputTxt;
        }


        // --- 액션 2: 코드로 비공개 방 참여 ---
        public async void OnConfirmJoinByCode( string code )
        {
            CommonUIController.Instance.ShowLoading();
            try
            {
                string title = await matchmakingService.JoinCustomLobbyByCodeAsync(code);
                
                if (title != null) EnterWaitingRoom(title, code);
                else lobbyView.UpdateStatus("접속 실패. 코드를 다시 확인하세요.");
            }
            finally
            {
                CommonUIController.Instance.DoneLoading();
            }
        }

        // --- 액션 3: 리스트에서 선택하여 참여 ---
        public async void OnConfirmJoinFromList(string lobbyId)
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
            
            // TODO : 대기실 UI 띄우기
            // TODO : 대기실 Code 만들어주기
            
            // lobbyView.SetWaitingRoomTitle(title);
            // lobbyView.SetWaitingRoomJoinCode(joinCode);
            // lobbyView.SwitchMainView(MainViewType.WaitingRoom);
            // // 🌟 1. 일단 손님 자리는 비워둠 ([+] 버튼 켜기)
            // lobbyView.SetGuestSlotActive(false);

            // 🌟 2. 네트워크 매니저가 켜져 있다면 실시간 이벤트 구독 시작!
            if (NetworkManager.Singleton != null)
            {
                // 내가 '손님'이면, 즉시 Guest UI 활성화
                if (!NetworkManager.Singleton.IsHost && NetworkManager.Singleton.IsClient)
                {
                    // lobbyView.SetGuestSlotActive(true);
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