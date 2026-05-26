using System;
using UnityEngine;
using UnityEngine.Events;

namespace DefaultNamespace
{
    public class LeftUpperController : MonoBehaviour
    {
        public static LeftUpperController Instance { get; private set; }

        private LeftUpper_Common ui_leftUpper;
        private UnityAction backAction = null;
        
        private void Awake() 
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterView( LeftUpper_Common ui ) {
            ui_leftUpper = ui;
        }

        public void RefreshUI() {
            ui_leftUpper.BindEvents();
        }


        public void OpenOptionUI() {
            if (isLobby()) {
                Debug.Log("[LeftUpperController] Open Lobby Option UI");
                UILoader.Instance.ShowUI("Option_Lobby_Popup");
            }
            else {
                Debug.Log("[LeftUpperController] Open InGame Option UI");
                UILoader.Instance.ShowUI("Option_InGame_Popup");
            }
        }
        
        public void OpenFriendUI() {
            Debug.Log("[LeftUpperController] Open Friend UI");
            UILoader.Instance.ShowUI("Friend_MainWindow");
        }

        public UnityAction GetAction_GoBack() {
            if ( UILoader.Instance.IsGoBackAllowed() ) {
                backAction = () => { UILoader.Instance.GoBack_FullScreen(); };
            }
            else {
                backAction = null;
            }
            
            return backAction;
        }
        
        private bool isLobby() {
            return true; // TODO
        }
    }
}
