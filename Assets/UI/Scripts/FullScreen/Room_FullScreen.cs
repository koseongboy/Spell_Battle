using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
        [SerializeField] private GameObject hostSlotGroup;
        [SerializeField] private GameObject guestSlotGroup;
        
        [SerializeField] private TextMeshProUGUI txt_HostName;
        [SerializeField] private TextMeshProUGUI txt_GuestName;
        // [SerializeField] private Image img_HostProfile;
        // [SerializeField] private Image img_GuestProfile;

        [Header("Lower Buttons")]
        [SerializeField] private Button btn_StartGame;
        [SerializeField] private Button btn_DeckList;
        [SerializeField] private Button btn_EditDeck;
        
        [SerializeField] private DeckList_Room_Popup deckListPopup;

        // Controller가 구독할 이벤트
        public event Action OnLeaveRoomClicked;
        public event Action OnStartGameClicked;
        public event Action OnDeckListClicked;

        private void Start()
        {
            if (RoomUIController.Instance != null)
            {
                RoomUIController.Instance.RegisterRoomUI(this);
            }

            // UI 클릭 이벤트를 Controller로 전달
            btn_StartGame.onClick.AddListener(() => OnStartGameClicked?.Invoke());
            btn_DeckList.onClick.AddListener(() => OnDeckListClicked?.Invoke());
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
        public void SetHostInfo(/* 매개변수로 플레이어 데이터 객체 전달 */)
        {
            hostSlotGroup.SetActive(true);
            
            // TODO : 플레이어 정보 불러오기 (Name, Score, Rank 등)
            txt_HostName.text = "Host Player"; // 임시
        }

        // 게스트(손님) 정보 세팅
        public void SetGuestInfo(/* 매개변수로 플레이어 데이터 객체 전달 */)
        {
            guestSlotGroup.SetActive(true);
            
            // TODO : 플레이어 정보 불러오기 (Name, Score, Rank 등)
            txt_GuestName.text = "Guest Player"; // 임시
        }

        // 게스트가 나갔을 때 슬롯 비우기
        public void ClearGuestInfo()
        {
            guestSlotGroup.SetActive(false);
            txt_GuestName.text = string.Empty;
            
            // TODO : 초대하기 버튼 다시 띄우기
        }

        // 방장에게만 게임 시작 버튼 활성화
        public void SetStartButtonActive(bool isActive)
        {
            btn_StartGame.gameObject.SetActive(isActive);
        }
        

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
