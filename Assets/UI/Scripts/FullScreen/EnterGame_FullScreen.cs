using System;
using System.Collections.Generic;
using Controllers.LobbyController;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Pool;

namespace DefaultNamespace
{
    public enum EnterGame_UIMode {
        CreateRoom,
        FindRoom,
        Other
    }
    
    public class EnterGame_FullScreen : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        private EnterGame_UIMode mode = EnterGame_UIMode.CreateRoom;
        private bool isCreatingRoomPrivate = false;

        [Header("Buttons")]
        [SerializeField] private Button btn_RandomEnter;
        [SerializeField] private Button btn_CreateRoom;
        [SerializeField] private Button btn_FindRoom;
        [SerializeField] private Button btn_SearchRoom;
        
        [Header("Images")]
        [SerializeField] private Image CreateRoom_BtnImage;
        [SerializeField] private Image FindRoom_BtnImage;

        [SerializeField] private Sprite Default_BtnSprite;
        [SerializeField] private Sprite Seleted_BtnSprite;
        
        [Header("Create Room")]
        [SerializeField] private TMP_InputField input_RoomName;
        [SerializeField] private Button btn_ConfirmCreateRoom;
        [SerializeField] private Button btn_PublicToggle;
        [FormerlySerializedAs("rt_CreateRoom_PublicToggle")] [SerializeField] private RectTransform rt_CreateRoom_PrivateToggle;
        
        [Header("Find Room")]
        [SerializeField] private Transform contentParent;    // Scroll View 안의 Content 객체를 드래그 앤 드롭
        [SerializeField] private FindRoom_RoomPiece roomPiecePrefab;
        [SerializeField] private Button btn_FindRoom_Enter;
        
        [Header("Search Room")]
        [SerializeField] private TMP_InputField input_RoomCode;
        
        // 현재 화면에 활성화되어 있는(풀에서 꺼낸) 아이템들을 추적하는 리스트
        private List<FindRoom_RoomPiece> activeItems = new List<FindRoom_RoomPiece>();
        // 오브젝트 풀 인터페이스 선언
        private IObjectPool<FindRoom_RoomPiece> roomPool;
        private string selectedLobbyId = string.Empty;
        
        
        [Header("Menu Element")]
        [SerializeField] private GameObject createRoomMenuElement;
        [SerializeField] private GameObject findRoomMenuElement;

        private Action<string> OnSearchRoomByCode;

        private void Start() {
            if (LobbyController.Instance != null) {
                LobbyController.Instance.RegisterEnterGameUI(this);
                BindEvents();
                SetMenuMode(EnterGame_UIMode.CreateRoom);
                ReadyRoomPiecePool();
            }
        }

        private void BindEvents() {
            var cont = LobbyController.Instance;
            
            // TODO : 랜덤입장
            // btn_RandomEnter.onClick.AddListener( () => cont. );
            
            // 좌측 메뉴
            btn_RandomEnter.onClick.AddListener( () => _ = cont.OnClick_RandomJoin() );
            
            btn_CreateRoom.onClick.AddListener( OnCreateRoomMenuPressed );
            
            btn_FindRoom.onClick.AddListener( OnFindRoomMenuPressed );
            btn_FindRoom.onClick.AddListener( cont.OnClick_FindRoom );
            
            btn_SearchRoom.onClick.AddListener( OnSearchRoomMenuPressed );
            OnSearchRoomByCode = cont.OnClick_JoinByCode;

            // Create Room
            btn_PublicToggle.onClick.AddListener(OnPublicTogglePressed);
            btn_ConfirmCreateRoom.onClick.AddListener( () => _ = cont.OnClick_CreateRoomConfirm());
            
            // Find Room
            btn_FindRoom_Enter.onClick.AddListener( () => cont.OnClick_JoinFromList(selectedLobbyId) );
        }
        

        private void OnCreateRoomMenuPressed() {
            SetMenuMode(EnterGame_UIMode.CreateRoom);
        }

        private void OnFindRoomMenuPressed() {
            SetMenuMode(EnterGame_UIMode.FindRoom);
        }

        private void OnSearchRoomMenuPressed() {
            string roomCode = GetInput_RoomCode();
            if (string.IsNullOrEmpty(roomCode)) { CommonUIController.Instance.ShowRedAlert("코드를 입력하세요!");
                return;
            }
            
            OnSearchRoomByCode(roomCode);
        }

        private void OnPublicTogglePressed() {
            isCreatingRoomPrivate = !isCreatingRoomPrivate;
            // 이동할 목표 X 좌표 설정
            float targetX = isCreatingRoomPrivate ? 120f : -120f;
    
            // 기존에 실행 중인 동일 객체의 트윈을 취소 (빠른 연타 버그 방지)
            rt_CreateRoom_PrivateToggle.DOKill();
    
            // 0.2초 동안 X 좌표를 targetX로 이동하며, 통통 튀는 텐션(OutBack) 부여
            rt_CreateRoom_PrivateToggle.DOAnchorPosX(targetX, 0.2f).SetEase(Ease.OutQuint);
        }


        private void SetMenuMode( EnterGame_UIMode newMode ) {
            mode = newMode;

            if (mode == EnterGame_UIMode.CreateRoom) {
                CreateRoom_BtnImage.sprite = Seleted_BtnSprite;
                FindRoom_BtnImage.sprite = Default_BtnSprite;
                
                createRoomMenuElement.gameObject.SetActive(true);
                findRoomMenuElement.gameObject.SetActive(false);
            }
            else {
                CreateRoom_BtnImage.sprite = Default_BtnSprite;
                FindRoom_BtnImage.sprite = Seleted_BtnSprite;
                
                createRoomMenuElement.gameObject.SetActive(false);
                findRoomMenuElement.gameObject.SetActive(true);
            }
        }

        public void UpdateUI_RoomList(List<Lobby> lobbies) {
            Debug.Log(lobbies);
            
            // 1. 기존 리스트 초기화 (Destroy 대신 풀에 반환)
            foreach (FindRoom_RoomPiece item in activeItems)
            {
                roomPool.Release(item);
            }
            activeItems.Clear(); // 추적 리스트 비우기

            // 2. 새 리스트로 채워넣기
            foreach (Lobby lobby in lobbies) 
            {
                // 풀에서 잠자고 있는 UI 객체를 하나 가져옴 (부족하면 createFunc 자동 실행)
                FindRoom_RoomPiece newItem = roomPool.Get();
                // 프리팹 내부의 텍스트 및 데이터 갱신
                newItem.SetUp(lobby, SetSelectedLobbyId);
                // 관리용 추적 리스트에 추가
                activeItems.Add(newItem);
            }
        }

        public void SetSelectedLobbyId(string lobbyId) {
            selectedLobbyId = lobbyId;
        }

        
        // 방 생성 - 입력된 방 이름을 가져오는 함수
        public string GetInput_RoomName() {
            string inputTxt = input_RoomName.text;
            return inputTxt;
        }

        // 방 생성 - 방 Public / Private 토글을 가져오는 함수
        public bool GetInput_IsRoomPrivate() {
            return isCreatingRoomPrivate;
        }

        // 방 코드 입력 - 입력된 방 코드 가져오는 함수
        public string GetInput_RoomCode() {
            string inputTxt = input_RoomCode.text;
            return inputTxt;
        }


        private void ReadyRoomPiecePool() {
            // 씬 시작 시 오브젝트 풀 초기화 및 규칙 셋업
            roomPool = new ObjectPool<FindRoom_RoomPiece>(
                createFunc: () => Instantiate(roomPiecePrefab, contentParent), // 1. 풀에 여분이 없을 때 새로 생성하는 로직
                actionOnGet: (item) => item.gameObject.SetActive(true),   // 2. 풀에서 꺼낼 때 실행할 로직 (활성화)
                actionOnRelease: (item) => item.gameObject.SetActive(false), // 3. 풀로 반환할 때 실행할 로직 (비활성화)
                actionOnDestroy: (item) => Destroy(item.gameObject),      // 4. 최대 보관 용량 초과 시 파괴 로직
                defaultCapacity: 5, // 기본 할당량
                maxSize: 200          // 최대 보관량 (이 수치를 넘어가면 반환 시 객체를 파괴함)
            );
        }
    }
}
