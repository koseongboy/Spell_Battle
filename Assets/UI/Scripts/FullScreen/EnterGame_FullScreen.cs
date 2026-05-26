using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public enum EnterGame_UIMode {
        CreateRoom,
        FindRoom
    }
    
    public class EnterGame_FullScreen : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.FullScreen;
        
        [SerializeField] private Image CreateRoom_BtnImage;
        [SerializeField] private Image FindRoom_BtnImage;

        [SerializeField] private Sprite Default_BtnSprite;
        [SerializeField] private Sprite Seleted_BtnSprite;

        public void RandomEnter_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Random Enter Pressed");
        }

        public void CreateRoom_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Create Room Pressed");
        }

        public void FindRoom_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Find Room Pressed");
        }

        public void SearchRoom_Pressed() {
            Debug.Log("[EnterGame_FullScreen] Search Room Pressed");
        }
    }
}
