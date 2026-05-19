using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using TMPro;

namespace Views.LobbyView
{
    // 화면의 큰 틀 (로비 화면이냐, 대결방 화면이냐)
    public enum MainViewType { Lobby, WaitingRoom }
    
    // 로비 화면 내부의 탭 (만들기냐, 찾기냐)
    public enum LobbyTabType { Create, Search }

    public class LobbyView : MonoBehaviour
    {
        [Header("개발용 룸 아이디 보기")]
        [SerializeField] private string selectedLobbyId; // 현재 유저가 클릭한 방의 ID 보관용
        [Header("화면 패널 (활성화 제어용)")]
        public GameObject lobbyMainPanel;   // 로비 전체 (사이드바 + 중앙창)
        public GameObject waitingRoomPanel; // 대결방 전체

        [Header("글로벌 상단 메뉴 (항상 보임)")]
        public Button settingButton;
        public Button friendButton;
        public Button globalBackButton; // 상황에 따라 타이틀로 가거나 방을 나감

        [Header("사이드바 메뉴 (로비 전용)")]
        public Button tabCreateModeButton; // 대결 만들기 탭으로 이동
        public Button tabSearchModeButton; // 대결 찾기 탭으로 이동
        public Button joinRoomByCodeButton; // [액션] 코드 입력 후 즉시 참여 버튼
        public TMP_InputField joinCodeInput;  // 사이드바 - 참여 코드
        
        [Header("대결 만들기 탭")]
        public GameObject createPanel; // 대결 만들기 화면
        public TMP_InputField roomTitleInput; // 대결 만들기 - 방 제목
        public Toggle privateToggle;          // 대결 만들기 - 비공개 토글
        public Button confirmCreateButton; // [액션] 방 생성 및 시작 버튼

        [Header("대결 찾기 탭")]
        public Button confirmJoinFromListButton; // [액션] 리스트 선택 후 입장 버튼
        public GameObject searchPanel; // 대결 찾기 화면
        public Transform listContainer;              // 리스트 아이템들이 생성될 부모 (Content)
        public GameObject roomItemPrefab;            // 리스트 1칸 프리팹

        [Header("대결방 (Waiting Room)")]
        public TextMeshProUGUI waitingRoomTitle; // 방 입장 후 방제
        public TextMeshProUGUI waitingRoomJoinCode; // 방 입장 후 방 코드
        public Button readyOrStartButton; // 준비 또는 게임 시작 버튼
        public Button deckSelectButton; // 덱/콘셉트 선택 버튼
        public GameObject emptyAddButton; // [+] 버튼
        public GameObject guestInfoGroup; // 손님 아바타/정보 그룹

        [Header("로딩 UI 연결")]
        public GameObject loadingPanel; // 화면 전체를 덮는 패널 오브젝트
        public TextMeshProUGUI loadingStatusText; // 로딩 중 표시할 안내 글씨



        // --- 화면 스위칭 ---
        public void SwitchMainView(MainViewType viewType)
        {
            lobbyMainPanel.SetActive(viewType == MainViewType.Lobby);
            waitingRoomPanel.SetActive(viewType == MainViewType.WaitingRoom);
        }

        public void ShowLobbyTab(LobbyTabType tabType)
        {
            createPanel.SetActive(tabType == LobbyTabType.Create);
            searchPanel.SetActive(tabType == LobbyTabType.Search);
        }
        public void SetGuestSlotActive(bool isGuestPresent)
        {
            if (emptyAddButton != null) emptyAddButton.SetActive(!isGuestPresent);
            if (guestInfoGroup != null) guestInfoGroup.SetActive(isGuestPresent);
        }
        public void UpdateStatus(string message) => Debug.Log($"[Lobby] {message}"); // 로깅용

        public string GetRoomTitle() => roomTitleInput.text;
        public bool GetIsPrivate() => privateToggle.isOn;
        public string GetInputCode() => joinCodeInput.text.ToUpper();

        public void SetWaitingRoomTitle(string title) => waitingRoomTitle.text = title;
        public void SetWaitingRoomJoinCode(string code) => waitingRoomJoinCode.text = code;
        public void SetLoadingPanel(bool isActive, string statusMessage = "")
        {
            if (loadingPanel != null) 
                loadingPanel.SetActive(isActive);

            if (loadingStatusText != null && isActive) 
                loadingStatusText.text = statusMessage;
        }

        public string GetSelectedLobbyId() => selectedLobbyId;

        

        // --- 방 리스트 그리기 ---
        public void RenderRoomList(List<Lobby> lobbies)
        {
            // 1. 기존 리스트 초기화
            foreach (Transform child in listContainer) Destroy(child.gameObject);
            selectedLobbyId = null;

            // 2. 새로운 리스트 생성
            foreach (var lobby in lobbies)
            {
                var itemObj = Instantiate(roomItemPrefab, listContainer);
                RoomItem item = itemObj.GetComponent<RoomItem>();
                item.Setup(lobby, OnRoomSelected); 
            }
        }

        // 리스트에서 특정 방을 클릭했을 때 호출될 함수 (RoomItem 프리팹에서 이걸 찔러줘야 함)
        public void OnRoomSelected(string lobbyId, string lobbyName)
        {
            selectedLobbyId = lobbyId;
        }
    }
}