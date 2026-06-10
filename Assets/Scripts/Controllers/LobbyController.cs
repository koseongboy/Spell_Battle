using System;
using System.Text.RegularExpressions;
using UnityEngine;
using Models.RelayMatchmakingService;
using Views.LobbyView;
using Unity.Netcode;
using System.Threading.Tasks;
using DefaultNamespace;

namespace Controllers.LobbyController {
    public class LobbyController : MonoBehaviour {
        public static LobbyController Instance { get; private set; }

        private RelayMatchmakingService matchmakingService;
        [SerializeField] private LobbyView lobbyView;

        private Lobby_FullScreen ui_Lobby;
        private EnterGame_FullScreen ui_EnterGame;

        // 현재 유저가 대결방에 들어가 있는지 여부를 추적 (뒤로가기 버튼 로직 처리용)
        private bool isInWaitingRoom = false;

        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            matchmakingService = RelayMatchmakingService.Instance;
        }

        private void OnEnable() {
            // 혹시 있을까 꺼주기
            
            try
            {
                CommonUIController.Instance.DoneLoading();
            } catch (Exception e)
            {
                Debug.LogWarning($"이런 메세지가 떳지만 무시했어요 \n {e.Message}");
            }
            
        }

        /// <summary>
        /// 웹 서버 로그인이 완전히 끝난 후, 외부에서 명시적으로 호출할 UGS 및 릴레이 초기화 함수
        /// </summary>
        public async Task InitializeNetworkAsync() {
            CommonUIController.Instance.ShowLoading();
            Debug.Log("[LobbyController] 웹 로그인 인증 확인 완료. UGS 및 릴레이 서비스 세션을 초기화합니다.");

            // 서버 로그인 및 익명 세션 발급 진행 (RelayMatchmakingService 내부 함수 호출)
            await matchmakingService.InitializeAndSignInAsync();

            Debug.Log("[LobbyController] 네트워킹 초기화 및 익명 로그인 완료.");
            CommonUIController.Instance.DoneLoading();
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


        // '게임 시작'
        public void OnGameStartPressed() {
            if (ui_Lobby == null) {
                return;
            }

            CommonUIController.Instance.ChangeFullScreen("EnterGame_FullScreen");
        }

        // '덱'
        public void OnDeckPressed() {
            CommonUIController.Instance.ChangeFullScreen("DeckEdit_FullScreen");
        }

        // '튜토리얼'
        public void OnTutorialPressed() {
            CommonUIController.Instance.ShowBlackAlert("미구현입니다. 첨부된 문서를 확인해주세요.");
        }

        // '크레딧'
        public void OnCreditPressed() {
            CommonUIController.Instance.ShowBlackAlert("CAU Software 고성현, 김명준, 김이경");
        }



        // --- 탭 이동: 방 찾기 ---
        public async void OnClick_FindRoom() {
            if (lobbyView == null) return;

            CommonUIController.Instance.ShowLoading();

            var lobbies = await matchmakingService.GetPublicLobbyListAsync();

            CommonUIController.Instance.DoneLoading();
            ui_EnterGame.UpdateUI_RoomList(lobbies);
        }

        // [추가] 로딩창 없이 뒤에서 조용히 방 리스트만 갱신하는 함수
        public async void RefreshRoomListSilent() {
            if (lobbyView == null || ui_EnterGame == null) return;

            var lobbies = await matchmakingService.GetPublicLobbyListAsync();
            ui_EnterGame.UpdateUI_RoomList(lobbies);
        }

        // --- 액션 : 랜덤 방 진입 ---
        public async Task OnClick_RandomJoin() {
            CommonUIController.Instance.ShowLoading();

            var (isHost, joinCode) = await matchmakingService.QuickMatchAsync();
            // 3. 반환된 릴레이 접속 코드(joinCode)가 정상적으로 존재하는지 체크
            if (!string.IsNullOrEmpty(joinCode)) {
                Debug.Log($"[랜덤 매치 성공] Host 여부: {isHost} | JoinCode: {joinCode}");

                // 4. 기존에 이미 구현되어 있는 대기실 진입 함수 호출
                RoomUIController.Instance.EnterRoom();
            }

            if (joinCode != null) {
                if (isHost) {
                    Debug.Log("기존 방이 없어 새로운 방의 방장(Host)으로 매칭을 대기합니다.");
                }
                else {
                    Debug.Log("기존에 존재하던 방에 게스트(Client)로 매칭되었습니다.");
                }
            }
            else {
                CommonUIController.Instance.ShowRedAlert("오류가 발생했습니다. 다시 시도해주세요.");
            }

            CommonUIController.Instance.DoneLoading();
        }

        // --- 액션 1: 커스텀 방 생성 ---
        public async Task OnClick_CreateRoomConfirm() {

            if (ui_EnterGame == null) {
                Debug.LogError("[CreateRoom] ui_EnterGame이 null입니다! 뷰 등록이 정상적으로 되지 않았습니다.");
                return;
            }

            string rawTitle = ui_EnterGame.GetInput_RoomName();
            bool isPrivate = ui_EnterGame.GetInput_IsRoomPrivate();

            if (string.IsNullOrEmpty(rawTitle)) {
                CommonUIController.Instance.ShowRedAlert("방 제목을 입력하세요!");
                return;
            }

            string title = FilterTitle(rawTitle);
            Debug.Log($"[CreateRoom] 2. 필터링된 방 제목: '{title}'");

            // 특수문자만 입력해서 필터링 후 빈 문자열이 된 경우 방어
            if (string.IsNullOrEmpty(title)) {
                CommonUIController.Instance.ShowRedAlert("유효하지 않은 방 제목입니다.");
                return;
            }

            CommonUIController.Instance.ShowLoading();

            try {
                if (RoomUIController.Instance == null) {
                    CommonUIController.Instance.ShowRedAlert("시스템 오류: 대기실 컨트롤러를 찾을 수 없습니다.");
                    return;
                }

                RoomUIController.Instance.SetupNetworkCallbacks();

                string lobbyCode =
                    await matchmakingService.CreateCustomLobbyAsync(title, isPrivate);

                if (!string.IsNullOrEmpty(lobbyCode)) {
                    RoomUIController.Instance.EnterRoom();
                }
                else {
                    CommonUIController.Instance.ShowRedAlert("방 생성에 실패했습니다. 네트워크를 확인해주세요.");
                }
            }
            catch (Exception e) {
                Debug.LogError($"[CreateRoom] 치명적 예외(Exception) 발생: {e.Message}\n{e.StackTrace}");
                CommonUIController.Instance.ShowRedAlert("방 생성 중 오류가 발생했습니다.");
            }
            finally {
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
            inputTxt = Regex.Replace(inputTxt, @"[^a-zA-Z0-9가-힣ㄱ-ㅎㅏ-ㅣ\s!?\-_]", "");

            return inputTxt;
        }


        // --- 액션 2: 코드로 비공개 방 참여 ---
        public async void OnClick_JoinByCode(string code) {
            Debug.Log(code);
            CommonUIController.Instance.ShowLoading();
            try {
                string title = await matchmakingService.JoinCustomLobbyByCodeAsync(code);

                if (title != null) RoomUIController.Instance.EnterRoom();
                else CommonUIController.Instance.ShowRedAlert("접속 실패. 코드를 다시 확인하세요.");
            }
            finally {
                CommonUIController.Instance.DoneLoading();
            }
        }

        // --- 액션 3: 리스트에서 선택하여 참여 ---
        public async void OnClick_JoinFromList(string lobbyId) {
            if (string.IsNullOrEmpty(lobbyId)) {
                CommonUIController.Instance.ShowRedAlert("입장할 방을 선택하세요.");
                return;
            }

            CommonUIController.Instance.ShowLoading();
            string title = await matchmakingService.JoinCustomLobbyByIdAsync(lobbyId);
            CommonUIController.Instance.DoneLoading();

            if (title != null) {
                RoomUIController.Instance.EnterRoom();
            }
            else {
                CommonUIController.Instance.ShowRedAlert("지금은 닫힌 방입니다.");
                RefreshRoomListSilent();
            }
        }
    }
}