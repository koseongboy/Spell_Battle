using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class Room_FullScreen : MonoBehaviour, UI_ILayerInfo
    {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        [Header("Room Info UI")]
        [SerializeField] private TextMeshProUGUI txt_RoomTitle;
        [SerializeField] private TextMeshProUGUI txt_RoomCode;

        [Header("Player Slots UI")]
        [Header("Host")]
        [SerializeField] private GameObject hostSlotGroup;
        [SerializeField] private TextMeshProUGUI txt_HostName;
        [SerializeField] private TextMeshProUGUI txt_HostRank;
        [SerializeField] private TextMeshProUGUI txt_HostScore;

        [Header("Guest")]
        [SerializeField] private GameObject guestSlotGroup;
        [SerializeField] private TextMeshProUGUI txt_GuestName;
        [SerializeField] private TextMeshProUGUI txt_GuestRank;
        [SerializeField] private TextMeshProUGUI txt_GuestScore;
        [SerializeField] private GameObject inviteButton;

        
        [FormerlySerializedAs("btn_Game")]
        [Header("Lower Buttons")]
        [SerializeField] private Button btn_GameStart;
        [SerializeField] private Button btn_Ready;
        [SerializeField] private TextMeshProUGUI txt_ReadyBtnText;
        [SerializeField] private Button btn_DeckList;
        [SerializeField] private Button btn_EditDeck;
        
        [SerializeField] private DeckList_Room_Popup deckListPopup;

        // Controller가 구독할 이벤트
        public event Action OnLeaveRoomClicked;
        public event Action OnStartGameClicked;
        public event Action OnDeckListClicked;
        public event Action OnEditDeckClicked;
        public event Action OnReadyClicked;
        

        private void Start()
        {
            if (RoomUIController.Instance != null)
            {
                RoomUIController.Instance.RegisterRoomUI(this);
            }

            // UI 클릭 이벤트를 Controller로 전달
            btn_GameStart.onClick.AddListener(() => OnStartGameClicked?.Invoke());
            btn_Ready.onClick.AddListener(() => OnReadyClicked?.Invoke());
            btn_DeckList.onClick.AddListener(() => OnDeckListClicked?.Invoke());
            btn_EditDeck.onClick.AddListener(() => OnEditDeckClicked?.Invoke());
            
            // 팝업 초기 상태는 비활성화
            if (deckListPopup != null)
            {
                deckListPopup.gameObject.SetActive(false);
            }
        }

        
        // ==========================================
        // 1 & 2. 방 정보 UI 업데이트 (Controller가 호출해 줌)
        // ==========================================
        public void UpdateRoomInfo(string roomName, string roomCode)
        {
            txt_RoomTitle.text = roomName;
            txt_RoomCode.text = roomCode;
        }

        
        // ==========================================
        // 3. 플레이어 슬롯 UI 업데이트
        // ==========================================
        
        // 호스트(방장) 정보 세팅
        public void UpdateHostUI(/* 매개변수로 플레이어 데이터 객체 전달 */)
        {
            hostSlotGroup.SetActive(true);
            
            // TODO
            txt_HostName.text = "Host Player";
            txt_HostRank.text = "5";
            txt_HostScore.text = "12345";
        }

        // 게스트(손님) 정보 세팅
        public void UpdateGuestUI(/* 매개변수로 플레이어 데이터 객체 전달 */)
        {
            inviteButton.SetActive(false);
            guestSlotGroup.SetActive(true);
            
            // TODO
            txt_GuestName.text = "Guest Player";
            txt_GuestRank.text = "3";
            txt_GuestScore.text = "54321";
        }

        // 게스트가 나갔을 때 슬롯 비우기
        public void ClearGuestUI()
        {
            guestSlotGroup.SetActive(false);
            txt_GuestName.text = string.Empty;
            txt_GuestRank.text = string.Empty;
            txt_GuestScore.text = string.Empty;
            
            inviteButton.SetActive(true);
        }

        // 방장에게만 게임 시작 버튼 활성화
        public void SetStartButtonActive(bool isActive)
        {
            btn_GameStart.gameObject.SetActive(isActive);
        }
        
        // ==========================================
        // 게임 시작 & 준비 완료
        // ==========================================
        
        public void SetupRoleButtons(bool isHost)
        {
            btn_GameStart.gameObject.SetActive(isHost); // 방장만 시작 버튼 노출
            btn_Ready.gameObject.SetActive(!isHost);    // 손님만 준비 버튼 노출
        }

        // Host의 게임 시작버튼 활성화 비활성화
        public void SetStartButtonInteractable(bool isInteractable)
        {
            btn_GameStart.interactable = isInteractable;
        }

        // Guest 레디버튼 업데이트
        public void UpdateReadyButtonVisual(bool isReady)
        {
            if (txt_ReadyBtnText != null)
            {
                txt_ReadyBtnText.text = isReady ? "준비 취소" : "준 비";
            }
        }
        
        
        
        // ==========================================
        // Deck 편집 관련
        // ==========================================

        // 덱 리스트 출력하는 함수
        public void OpenDeckListPopup(List<DeckMetaData> myDecks)
        {
            if (deckListPopup == null) return;
    
            // 비활성화 상태라면 켜줌
            if (!deckListPopup.gameObject.activeSelf)
            {
                deckListPopup.gameObject.SetActive(true);
            }
    
            deckListPopup.UpdateDeckListUI(myDecks);
            deckListPopup.ShowPopup();
        }
        
        public void CloseDeckListPopup()
        {
            if (deckListPopup == null) return;

            deckListPopup.HidePopup(); // 닫기 애니메이션 실행
        }
    }
}
