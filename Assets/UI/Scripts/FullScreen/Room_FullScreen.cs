using System;
using System.Collections.Generic;
using Models.CardDatabases;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class Room_FullScreen : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        [Header("Room Info UI")] [SerializeField]
        private TextMeshProUGUI txt_RoomTitle;

        [SerializeField] private TextMeshProUGUI txt_RoomCode;

        [Header("Player Slots UI")] [Header("Host")] [SerializeField]
        private GameObject hostSlotGroup;

        [SerializeField] private TextMeshProUGUI txt_HostName;
        [SerializeField] private TextMeshProUGUI txt_HostRank;
        [SerializeField] private TextMeshProUGUI txt_HostScore;

        [Header("Guest")] [SerializeField] private GameObject guestSlotGroup;
        [SerializeField] private TextMeshProUGUI txt_GuestName;
        [SerializeField] private TextMeshProUGUI txt_GuestRank;
        [SerializeField] private TextMeshProUGUI txt_GuestScore;
        [SerializeField] private GameObject img_GuestReadyCheck;


        [Header("Lower Buttons")] [SerializeField]
        private Button btn_GameStart;

        [SerializeField] private Image img_GameStart;
        [SerializeField] private Button btn_Ready;
        [SerializeField] private Image img_Ready;
        [SerializeField] private TextMeshProUGUI txt_ReadyBtnText;
        [SerializeField] private Button btn_DeckList;
        [SerializeField] private Button btn_EditDeck;
        [SerializeField] private Sprite img_active;
        [SerializeField] private Sprite img_inactive;

        [Header("Selected Deck UI")] [SerializeField]
        private DeckList_Room_Popup deckListPopup;

        public TextMeshProUGUI txt_SelectedDeckName;
        public TextMeshProUGUI txt_SelectedDeckSummary;
        public Image img_SelectedDeckElement;


        // Controller가 구독할 이벤트
        public event Action OnLeaveRoomClicked;
        public event Action OnStartGameClicked;
        public event Action OnDeckListClicked;
        public event Action OnEditDeckClicked;
        public event Action OnReadyClicked;


        private void Start() {
            if (RoomUIController.Instance != null) {
                RoomUIController.Instance.RegisterRoomUI(this);
            }

            // UI 클릭 이벤트를 Controller로 전달
            btn_GameStart.onClick.AddListener(() => OnStartGameClicked?.Invoke());
            btn_Ready.onClick.AddListener(() => OnReadyClicked?.Invoke());
            btn_DeckList.onClick.AddListener(() => OnDeckListClicked?.Invoke());
            btn_EditDeck.onClick.AddListener(() => OnEditDeckClicked?.Invoke());

            // 팝업 초기 상태는 비활성화
            if (deckListPopup != null) {
                deckListPopup.gameObject.SetActive(false);
            }
        }

        private void OnEnable() {
            // 🌟 대기실 화면이 켜질 때마다 뒤로 가기를 '방 퇴장 로직'으로 덮어씌움
            if (LeftUpperController.Instance != null) {
                LeftUpperController.Instance.SetBackAction(() => {
                    RoomUIController.Instance.OnBackButtonPressedInRoom();
                });
            }
        }


        // ==========================================
        // 1 & 2. 방 정보 UI 업데이트 (Controller가 호출해 줌)
        // ==========================================
        public void UpdateRoomInfo(string roomName, string roomCode) {
            txt_RoomTitle.text = roomName;
            txt_RoomCode.text = roomCode;
        }


        // ==========================================
        // 3. 플레이어 슬롯 UI 업데이트
        // ==========================================

        public void ResetRoomUI() {
            txt_RoomTitle.text = string.Empty;
            txt_RoomCode.text = string.Empty;

            // 게스트 슬롯 숨기기 및 텍스트 초기화
            ClearGuestUI();

            // 버튼 상태 및 체크 이미지 초기화
            UpdateReadyButton(false);
            UpdateGuestReadyImg(false);
            UpdateStartButton(false);
        }

        // 호스트(방장) 정보 세팅
        public void UpdateHostUI(string name, int score, string rank) {
            hostSlotGroup.SetActive(true);

            // TODO
            txt_HostName.text = name;
            txt_HostRank.text = rank;
            txt_HostScore.text = score.ToString();
        }

        // 게스트(손님) 정보 세팅
        public void UpdateGuestUI(string name, int score, string rank) {
            guestSlotGroup.SetActive(true);

            // TODO
            txt_GuestName.text = name;
            txt_GuestRank.text = rank;
            txt_GuestScore.text = score.ToString();
        }

        // 게스트가 나갔을 때 슬롯 비우기
        public void ClearGuestUI() {
            guestSlotGroup.SetActive(false);
            txt_GuestName.text = string.Empty;
            txt_GuestRank.text = string.Empty;
            txt_GuestScore.text = string.Empty;

            // 확실하게 이미지도 꺼줍니다.
            img_GuestReadyCheck.SetActive(false);
        }


        // ==========================================
        // 게임 시작 & 준비 완료
        // ==========================================

        public void SetupRoleButtons(bool isHost) {
            btn_GameStart.gameObject.SetActive(isHost); // 방장만 시작 버튼 노출
            btn_Ready.gameObject.SetActive(!isHost); // 손님만 준비 버튼 노출
        }

        // Host의 게임 시작버튼 활성화 비활성화
        public void UpdateStartButton(bool isInteractable) {
            btn_GameStart.interactable = isInteractable;
            img_GameStart.sprite = isInteractable ? img_active : img_inactive;
        }

        // Guest 레디버튼 업데이트
        public void UpdateReadyButton(bool isReady) {
            txt_ReadyBtnText.text = isReady ? "준비 취소" : "준 비";
            img_Ready.sprite = isReady ? img_active : img_inactive;
        }

        public void UpdateGuestReadyImg(bool isReady) {
            img_GuestReadyCheck.SetActive(isReady);
        }


        // ==========================================
        // Deck 편집 관련
        // ==========================================

        // 팝업에서 덱 선택 시 호출될 메인 UI 업데이트 함수
        public void UpdateSelectedDeckUI(string deckName, string summary, Cards.CardUIDatas.Property repProp) {
            txt_SelectedDeckName.text = deckName;

            txt_SelectedDeckSummary.text = summary;
            
            img_SelectedDeckElement.sprite = CardDatabase.Instance.GetElementData(repProp).Icon;
        }

        // 덱 리스트 출력하는 함수
        public void OpenDeckListPopup(List<DeckMetaData> myDecks) {
            if (deckListPopup == null) return;

            // 비활성화 상태라면 켜줌
            if (!deckListPopup.gameObject.activeSelf) {
                deckListPopup.gameObject.SetActive(true);
            }

            deckListPopup.UpdateDeckListUI(myDecks);
            deckListPopup.ShowPopup();
        }

        public void CloseDeckListPopup() {
            if (deckListPopup == null) return;

            deckListPopup.HidePopup(); // 닫기 애니메이션 실행
        }
    }
}