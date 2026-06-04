using System.Collections;
using UnityEngine;

namespace Managers.VoiceManagers
{
    public class VoicePlayer : MonoBehaviour
    {
        public AudioSource audioSource;

        public void PlayFromUrl(string url, float volume)
        {
            audioSource.volume = volume;
            StartCoroutine(DownloadAndPlay(url));
        }

        private IEnumerator DownloadAndPlay(string url)
        {
            using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    audioSource.clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    audioSource.Play();
                }
            }
        }
    }
}
