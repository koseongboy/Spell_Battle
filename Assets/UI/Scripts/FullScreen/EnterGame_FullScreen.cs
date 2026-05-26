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
        public LobbyController lobbyController;

        [SerializeField] private Button btn_RandomEnter;
        [SerializeField] private Button btn_CreateRoom;
        [SerializeField] private Button btn_FindRoom;
        
        
        [SerializeField] private Image CreateRoom_BtnImage;
        [SerializeField] private Image FindRoom_BtnImage;

        [SerializeField] private Sprite Default_BtnSprite;
        [SerializeField] private Sprite Seleted_BtnSprite;

        private void Start() {
            if (LobbyController.Instance != null) {
                LobbyController.Instance.RegisterEnterGameUI(this);
                BindEvents();
            }
        }

        private void BindEvents() {
            var cont = LobbyController.Instance;
            
            // randomEnterButton.onClick.AddListener( () => cont. );
            
        }


        public void RandomEnter_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Random Enter Pressed");
        }

        public void CreateRoom_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Create Room Pressed");
        }

        public void FindRoom_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Find Room Pressed");
        }


        public void ChangeMode_Create() {
            
        }

        public void ChangeMode_Find() {
            
        }

        public void UpdateUI_RoomList(List<Lobby> lobbies) {
            // TODO : 방 리스트 서버에서 가져오기
            // 
            
            // TODO : 기존 리스트 초기화
            
            // TODO : 새 리스트로 채워넣기
        }

        public void SearchRoom_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Search Room Pressed");
        }

        
        public string GetInput_RoomName() {
            throw new System.NotImplementedException();
        }

        public bool GetInput_IsRoomPublic() {
            throw new System.NotImplementedException();
        }
    }
}
