using UnityEngine;

namespace DefaultNamespace
{
    public class LeftUpper_Common : MonoBehaviour, UI_ILayerInfo
    {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        public void OpenOptionUI() {
            Debug.Log("[LeftUpper_Common] Open Option UI");
            UILoader.Instance.ShowUI("Option_Lobby_Popup");
        }
        
        public void OpenFriendUI() {
            Debug.Log("[LeftUpper_Common] Open Friend UI");
            UILoader.Instance.ShowUI("Friend_MainWindow");
        }

        public void GoBack() {
            Debug.Log("[LeftUpper_Common] Go Back Pressed");
        }
        
    }
}
