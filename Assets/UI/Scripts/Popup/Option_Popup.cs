using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class Option_Popup : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        [SerializeField] private GameObject lobbyUI;
        [SerializeField] private GameObject ingameUI;

        private bool isLobby = true;
        
        public void CloseUI() {
            UILoader.Instance.HideUI("Option_Lobby_Popup");
        }

        public void SurrenderPressed() {
            Debug.Log("[Option_Lobby] Surrender Pressed");
        }

        public void VoiceSettingPressed() {
            Debug.Log("[Option_Lobby] Voice Setting Pressed");
        }
        
        public void TutorialPressed() {
            Debug.Log("[Option_Lobby] Tutorial Pressed");
        }
        
        public void LogoutPressed() {
            Debug.Log("[Option_Lobby] Logout Pressed");
        }
        
        public void ExitGamePressed() {
            Debug.Log("[Option_Lobby] Exit Game Pressed");
        }


        private void OnEnable() {
            lobbyUI.SetActive(isLobby);
            ingameUI.SetActive(!isLobby);
        }

    }
}
