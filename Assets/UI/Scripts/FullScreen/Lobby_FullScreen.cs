using UnityEngine;

namespace DefaultNamespace
{
    public class Lobby_FullScreen : MonoBehaviour, UI_ILayerInfo
    {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        public void OpenCredit() {
            Debug.Log("[Lobby] Open Credit Pressed");
        }

        public void GameStartPressed() {
            Debug.Log("[Lobby] Game Start Pressed");
            // TODO : 화면 전환 연출
            UILoader.Instance.ShowUI("EnterGame_FullScreen");
            UILoader.Instance.HideUI("Lobby_FullScreen");
        }

        public void DeckEditPressed() {
            Debug.Log("[Lobby] Deck Edit Pressed");
        }

        public void TutorialPressed() {
            Debug.Log("[Lobby] Tutorial Pressed");
        }
    }
}
