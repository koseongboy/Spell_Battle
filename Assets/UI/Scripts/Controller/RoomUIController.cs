using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Models.RelayMatchmakingService;
using Unity.Netcode;
using UnityEngine;

namespace DefaultNamespace
{
    public class RoomUIController : MonoBehaviour
    {
        public static RoomUIController Instance { get; private set; }
        private RelayMatchmakingService matchmakingService;
        private Coroutine connectionTimeoutCoroutine;   // 현재 실행 중인 타이머를 기억할 변수

        // Deck 팝업이 열려있는지
        private bool isDeckPopupOpen = false;
        
        // View 컴포넌트 참조
        private Room_FullScreen ui_Room;
        [SerializeField] private ReadyStateModel readyStateModel;
        
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            matchmakingService = RelayMatchmakingService.Instance;

            SetupNetworkCallbacks();
        }
        
        // View가 생성되면서 자신을 Controller에 등록
        public void RegisterRoomUI(Room_FullScreen ui)
        {
            ui_Room = ui;
            
            // View 버튼들에 기능 주입
            ui_Room.OnLeaveRoomClicked += HandleLeaveRoom;
            ui_Room.OnStartGameClicked += HandleStartGame;
            ui_Room.OnReadyClicked += HandleReadyClicked;
            ui_Room.OnDeckListClicked += HandleDeckListClicked;
            ui_Room.OnEditDeckClicked += HandleDeckEditClicked;
            readyStateModel.OnGuestReadyChanged -= HandleGuestReadyStateChanged;
            readyStateModel.OnGuestReadyChanged += HandleGuestReadyStateChanged;
            
            SetupUI();
        }

        private void SetupUI() {
            bool isHost = NetworkManager.Singleton.IsHost;
            // 방장/손님 역할에 맞게 버튼 켜기
            ui_Room.SetupRoleButtons(isHost);
            

            // 초기 버튼 상태 세팅
            if (isHost) {
                // 방장은 처음엔 무조건 시작 불가 (손님이 없거나 준비를 안 했으므로)
                ui_Room.UpdateStartButton(false);
            } else {
                // 손님은 처음 들어왔을 때 무조건 준비 안 된 상태로 UI 세팅
                ui_Room.UpdateReadyButton(false);
            }

            ui_Room.UpdateGuestReadyImg(false);
            
            
            // Room Info 업데이트
            ui_Room.UpdateRoomInfo(matchmakingService.CurrentLobbyName, matchmakingService.CurrentLobbyCode);
            ui_Room.UpdateHostUI( /* TODO : 호스트의 정보 불러와서 주입해주기 */ );
            
            // 게스트 존재 여부 확인
            if (matchmakingService.HasGuest) {
                ui_Room.UpdateGuestUI( /* TODO : 게스트의 정보 불러와서 주입해주기 */ );
            }
            else {
                ui_Room.ClearGuestUI();
            }
        }

        public void EnterRoom()
        {
            // 1. 방 정보 갱신 (Model -> Controller -> View)
            CommonUIController.Instance.ShowLoading();
            CommonUIController.Instance.ChangeFullScreen("Room_FullScreen");

            if (NetworkManager.Singleton != null)
            {
                // 내가 '방장'이라면 게임 시작 버튼 노출
                if (NetworkManager.Singleton.IsHost)
                {
                    ui_Room?.SetupRoleButtons(true);
                }
                // 내가 '손님'으로 들어왔다면
                else if (NetworkManager.Singleton.IsClient)
                {
                    ui_Room?.SetupRoleButtons(false);
                    ui_Room?.UpdateGuestUI( /* TODO : 게스트의 정보 불러와서 주입해주기 */ );
                }
            }
            
            // 뒤로가기 버튼 세팅
            if (LeftUpperController.Instance != null)
            {
                LeftUpperController.Instance.SetBackAction(OnBackButtonPressedInRoom);
            }
            
            CommonUIController.Instance.DoneLoading();
        }
        
        
        #region View Event Handlers
        
        private async void HandleLeaveRoom()
        {
            CommonUIController.Instance.ShowLoading();
            await ReturnToLobbyMain();
            CommonUIController.Instance.DoneLoading();
        }

        private void HandleStartGame()
        {
            if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsList.Count == 2)
            {
                Debug.Log("게임 시작 씬으로 넘어갑니다!");
                // TODO: 씬 전환 또는 인게임 로직 호출 (NetworkManager.SceneManager.LoadScene 등)
            }
            else
            {
                CommonUIController.Instance.ShowRedAlert("게스트가 접속해야 시작할 수 있습니다.");
            }
        }
        
        // 게스트가 준비 버튼을 눌렀을 때
        private void HandleReadyClicked()
        {
            if (readyStateModel != null)
            {
                // Controller는 Model에게 명령(RPC)만 내림. 
                // 시각적 업데이트는 서버에서 값이 바뀐 후 콜백을 통해 이루어짐.
                readyStateModel.ToggleReadyServerRpc();
            }
        }

        // Model의 Ready 값이 바뀌었을 때
        private void HandleGuestReadyStateChanged(bool isReady)
        {
            if (ui_Room == null) return;
            
            

            if (NetworkManager.Singleton.IsHost)
            {
                // 방장이면: 손님의 상태에 따라 게임 시작 버튼의 잠금을 풀거나 채움
                ui_Room.UpdateStartButton(isReady);
            }
            else
            {
                // 손님이면: 내 화면의 버튼 텍스트를 "준비 취소" 혹은 "준비"로 바꿈
                ui_Room.UpdateReadyButton(isReady);
            }
            ui_Room.UpdateGuestReadyImg(isReady);
        }
        
        private void HandleDeckListClicked()
        {
            isDeckPopupOpen = !isDeckPopupOpen;

            if (isDeckPopupOpen) {
                // 열기
                List<DeckMetaData> currentDecks = GetStoredDeckData(); 
                if (ui_Room != null) {
                    ui_Room.OpenDeckListPopup(currentDecks);
                }
            }
            else {
                // 닫기
                if (ui_Room != null) {
                    ui_Room.CloseDeckListPopup();
                }
            }
        }

        private void HandleDeckEditClicked() {
            // TODO : 여기서 덱 편집 FullScreen 띄우기
            Debug.Log("[Room_FullScreen] Deck Edit Clicked. 아직 미구현입니다.");
        }

        #endregion View Event 
        
        private List<DeckMetaData> GetStoredDeckData()
        {
            // TODO : 저장소 또는 데이터 매니저로부터 실제 덱 정보 불러오기
            // 아 아아아
            
            // 테스트용 더미 데이터
            return new List<DeckMetaData>
            {
                new DeckMetaData { Name = "기본 불 덱", CardCount = "기본 30 불 10 생명 5", Element = DeckElement.Fire},
                new DeckMetaData { Name = "커스텀 물 덱", CardCount = "기본 26 불 14 생명 5", Element = DeckElement.Fire}
            };
        }
        
        // 좌상단 뒤로 가기 버튼이 눌렸을 때 실행될 래퍼(Wrapper) 함수
        private void OnBackButtonPressedInRoom()
        {
            // TODO : 진짜 나갈지 Confirm 팝업 
            _ = ReturnToLobbyMain(false);
        }
        
        #region Network
        // --- 기존 네트워크 콜백 이벤트들 ---
        public void SetupNetworkCallbacks()
        {
            if (NetworkManager.Singleton != null)
            {
                // StartHost가 불리기 '전'에 무조건 미리 세팅되어야 함!
                NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;

                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
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
                    // lobbyView.SetLoadingPanel(true, "상대 플레이어가 입장 중입니다...");
                    
                    if (connectionTimeoutCoroutine != null) StopCoroutine(connectionTimeoutCoroutine);
                    connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutRoutine(request.ClientNetworkId));
                    
                    CommonUIController.Instance.ShowLoading();
                }
            }

            // 2. 접속을 허가해 줍니다. (이 처리를 해야 OnClientConnected로 넘어갑니다)
            response.Approved = true;
            response.CreatePlayerObject = true; 
            response.Pending = false;
        }
        #endregion

        
        // 내가 방에서 나가는 코드
        private async Task ReturnToLobbyMain(bool isForce = false)
        {
            CommonUIController.Instance.ShowLoading();
            // 뒤로가기 버튼 반납
            if (LeftUpperController.Instance != null)
            {
                LeftUpperController.Instance.SetBackAction(null);
            }
            
            // 1. 이벤트 구독 해제 (중복 호출 방지)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                readyStateModel.OnGuestReadyChanged -= HandleGuestReadyStateChanged;
            }
            if(!isForce)
            {
                // 2. 서버 통신 및 네트워크 매니저 끄기 (LeaveLobbyAsync 내부에서 알아서 Host/Client 구분해 처리함)
                await matchmakingService.LeaveLobbyAsync();
            }
            // 3. 화면을 다시 로비 탭으로 스위칭
            CommonUIController.Instance.ChangeFullScreen("Lobby_FullScreen");
            CommonUIController.Instance.DoneLoading();
        }
        
                
        // 누군가 방에 접속 (Start와 같다 생각)
        private void OnClientConnected(ulong clientId)
        {
            // 무사히 접속을 완료했으므로 5초 타임아웃 타이머를 즉시 끕니다.
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
                CommonUIController.Instance.DoneLoading(); 
            }
            
            // 방장(나) 외에 누군가 들어왔다면 손님 UI 켜기
            if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsList.Count > 1)
            {
                ui_Room?.UpdateGuestUI( /* 게스트 정보 주입 필요 */ ); // 손님 들어옴 처리
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
                    ui_Room?.ClearGuestUI(); // 손님 UI 지우기
                }
            }
            else
            {
                // [손님 시점] 서버(방장)와의 연결이 끊어진 경우
                // (방장이 Shutdown을 하면 손님의 LocalClientId로 연결 끊김 콜백이 들어옵니다)
                if (clientId == NetworkManager.Singleton.LocalClientId || clientId == 0)
                {
                    CommonUIController.Instance.ShowBlackAlert("방이 삭제되어 퇴장합니다.");
                    await ReturnToLobbyMain(true); // 방 폭파 알림과 함께 강제 퇴장 처리
                }
            }
        }


        // ==========================================
        // 대기실에서 나갈 때 작동
        // ==========================================
        private void OnDestroy() {
            // 게임이 꺼지거나 오브젝트가 사라질 때, 
            // 돌고 있던 코루틴이나 비동기 작업들이 찌꺼기를 남기지 않도록 정리합니다.
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
            }
            
            if (matchmakingService != null)
            {
                Debug.Log("RoomUIController 파괴 감지: 로비에서 안전하게 퇴장 처리를 시도합니다.");
                
                // OnDestroy 내부에서는 async/await의 완벽한 대기를 보장할 수 없으므로,
                // Fire-and-Forget 형태로 무조건 서버에 '나 나간다'는 패킷을 던지고 프로세스를 종료합니다.
                _ = matchmakingService.LeaveLobbyAsync();
            }
        }
        
        // ==========================================
        // ⏱️ 5초 타임아웃 코루틴
        // ==========================================
        private System.Collections.IEnumerator ConnectionTimeoutRoutine(ulong clientId)
        {
            // 🌟 정확히 5초를 기다립니다.
            yield return new WaitForSeconds(5f);

            // --- 5초가 지났는데도 이 코드가 실행된다면? (연결 실패/지연) ---
            
            // Debug.LogWarning("상대방의 연결이 너무 오래 걸려 취소되었습니다.");
            
            // 1. 무한 로딩창 끄기
            CommonUIController.Instance.DoneLoading();
            CommonUIController.Instance.ShowBlackAlert("연결 상태가 불안정하여 취소되었습니다.");

            // 2. 혹시라도 비정상적으로 남아있을 손님 연결 강제 끊기
            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
            }
        }
        
        
        
        [ContextMenu("디버그: 현재 로비 강제 폭파")]
        public async void ForceKillLobby_Debug()
        {
            Debug.Log("강제로 로비를 폭파하고 하트비트를 정지합니다...");

            // 싱글톤 인스턴스가 존재할 때만 실행
            if (RelayMatchmakingService.Instance != null)
            {
                await RelayMatchmakingService.Instance.LeaveLobbyAsync();
                Debug.Log("로비 폭파 및 네트워크 셧다운 완료.");
            }
            else
            {
                Debug.LogWarning("현재 실행 중인 매치메이킹 서비스가 없습니다.");
            }
        }
    }
}
