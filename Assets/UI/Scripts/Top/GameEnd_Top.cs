using Models.RelayMatchmakingService;
using UnityEngine;
using UnityEngine.Serialization;

namespace DefaultNamespace
{
    public enum GameEndType {
        Win,
        Lose,
        Draw
    }
    
    
    public class GameEnd_Top : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<GameEndType>
    {
        public EUILayer TargetLayer => EUILayer.Top;

        public GameObject winUI;
        public GameObject loseUI;
        public GameObject drawUI;
        
        public void ReceiveData(GameEndType gameEndType) {
            winUI.SetActive(false);
            loseUI.SetActive(false);
            drawUI.SetActive(false);
            
            if (gameEndType == GameEndType.Win) {
                winUI.SetActive(true);
            }else if (gameEndType == GameEndType.Lose) {
                loseUI.SetActive(true);
            }else if (gameEndType == GameEndType.Draw) {
                drawUI.SetActive(true);
            }
        }

        public async void OnClick_Next() {
            Debug.Log("[GameEnd] 확인 버튼 클릭. 메인 로비로 돌아가는 로직 시작...");

            if (RelayMatchmakingService.Instance != null) {
                await RelayMatchmakingService.Instance.LeaveLobbyAsync();
            }

            // 2. Netcode(NGO) 안전 종료
            if (Unity.Netcode.NetworkManager.Singleton != null) {

                Unity.Netcode.NetworkManager.Singleton.Shutdown();

                await System.Threading.Tasks.Task.Delay(100);
                Destroy(Unity.Netcode.NetworkManager.Singleton.gameObject);
            }


            if (Managers.VoiceManagers.SoundManager.Instance != null) {
                Managers.VoiceManagers.SoundManager.Instance.ToggleBGM();
                if(Managers.VoiceManagers.SoundManager.Instance.isRecording) Managers.VoiceManagers.SoundManager.Instance.StopRecording();
            }

            Debug.Log("[GameEnd] 모든 네트워크 해제 완료. 로비 씬으로 이동합니다.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("01_Lobby_crocobob", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
