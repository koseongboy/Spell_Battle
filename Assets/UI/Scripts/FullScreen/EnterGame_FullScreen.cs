using System;
using System.Collections.Generic;
using Controllers.LobbyController;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public enum EnterGame_UIMode {
        CreateRoom,
        FindRoom
    }
    
    public class EnterGame_FullScreen : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        private EnterGame_UIMode mode = EnterGame_UIMode.CreateRoom;

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
            return ""; // TODO
        }

        public bool GetInput_IsRoomPublic() {
            return true; // TODO
        }
    }
}
