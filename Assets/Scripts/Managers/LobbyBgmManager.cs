using UnityEngine;

namespace DefaultNamespace
{
    public class LobbyBgmManager : MonoBehaviour
    {
        public AudioClip lobbyBGM;

        private void Start() {
            Managers.VoiceManagers.SoundManager.Instance.SetBgmAudioClip(lobbyBGM);

        }
    }
}
