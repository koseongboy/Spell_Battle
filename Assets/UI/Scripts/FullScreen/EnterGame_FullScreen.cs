using System;
using System.Collections.Generic;
using Controllers.LobbyController;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

namespace DefaultNamespace
{
    public enum EnterGame_UIMode {
        CreateRoom,
        FindRoom
    }
    
    public class EnterGame_FullScreen : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        private EnterGame_UIMode mode = EnterGame_UIMode.CreateRoom;
        private bool isCreatingRoomPublic = true;

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
        [SerializeField] private RectTransform rt_CreateRoom_PublicToggle;
        
        [Header("Menu Element")]
        [SerializeField] private GameObject createRoomMenuElement;
        [SerializeField] private GameObject findRoomMenuElement;

        private Action<string> OnSearchRoomByCode;

        private void Start() {
            if (LobbyController.Instance != null) {
                LobbyController.Instance.RegisterEnterGameUI(this);
                BindEvents();
                SetMenuMode(EnterGame_UIMode.CreateRoom);
            }
        }

        private void BindEvents() {
            var cont = LobbyController.Instance;
            
            // TODO : 랜덤입장
            // btn_RandomEnter.onClick.AddListener( () => cont. );
            
            
            btn_CreateRoom.onClick.AddListener( OnCreateRoomMenuPressed );
            
            btn_FindRoom.onClick.AddListener( OnFindRoomMenuPressed );
            btn_FindRoom.onClick.AddListener( cont.OnFindRoomPressed );
            
            btn_SearchRoom.onClick.AddListener( OnSearchRoomMenuPressed );

            OnSearchRoomByCode = cont.OnConfirmJoinByCode;

            btn_PublicToggle.onClick.AddListener(OnPublicTogglePressed);
            btn_ConfirmCreateRoom.onClick.AddListener( () => cont.OnConfirmCreateAsync());
        }
        

        private void OnCreateRoomMenuPressed() {
            SetMenuMode(EnterGame_UIMode.CreateRoom);
        }

        private void OnFindRoomMenuPressed() {
            SetMenuMode(EnterGame_UIMode.FindRoom);
        }

        private void OnSearchRoomMenuPressed() {
            string roomCode = GetInput_RoomName(); // TODO
            if (string.IsNullOrEmpty(roomCode)) { CommonUIController.Instance.ShowRedAlert("코드를 입력하세요!");
                return;
            }
            
            OnSearchRoomByCode(roomCode);
        }

        private void OnPublicTogglePressed() {
            isCreatingRoomPublic = !isCreatingRoomPublic;
            // 이동할 목표 X 좌표 설정
            float targetX = isCreatingRoomPublic ? -120f : 120f;
    
            // 기존에 실행 중인 동일 객체의 트윈을 취소 (빠른 연타 버그 방지)
            rt_CreateRoom_PublicToggle.DOKill();
    
            // 0.2초 동안 X 좌표를 targetX로 이동하며, 통통 튀는 텐션(OutBack) 부여
            rt_CreateRoom_PublicToggle.DOAnchorPosX(targetX, 0.2f).SetEase(Ease.OutQuint);
        }


        private void SetMenuMode( EnterGame_UIMode newMode ) {
            mode = newMode;

            if (mode == EnterGame_UIMode.CreateRoom) {
                createRoomMenuElement.gameObject.SetActive(true);
                findRoomMenuElement.gameObject.SetActive(false);
            }
            else {
                createRoomMenuElement.gameObject.SetActive(false);
                findRoomMenuElement.gameObject.SetActive(true);
            }
        }

        public void UpdateUI_RoomList(List<Lobby> lobbies) {
            // 방 리스트 서버에서 가져오기
            Debug.Log(lobbies);
            
            // TODO : 기존 리스트 초기화
            
            // TODO : 새 리스트로 채워넣기
        }

        
        public string GetInput_RoomName() {
            string inputTxt = input_RoomName.text;
            return inputTxt;
        }

        public bool GetInput_IsRoomPublic() {
            return isCreatingRoomPublic;
        }
    }
}
