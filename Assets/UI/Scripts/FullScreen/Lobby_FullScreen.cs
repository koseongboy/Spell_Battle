using UnityEngine;

namespace DefaultNamespace
{
    public class Lobby_FullScreen : MonoBehaviour
    {
        public void OpenCredit() {
            Debug.Log("[Lobby] Open Credit Pressed");
        }

        public void GameStartPressed() {
            Debug.Log("[Lobby] Game Start Pressed");
        }

        public void DeckEditPressed() {
            Debug.Log("[Lobby] Deck Edit Pressed");
        }

        public void TutorialPressed() {
            Debug.Log("[Lobby] Tutorial Pressed");
        }
    }
}
