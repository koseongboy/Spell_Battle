using System.Collections;
using Managers.VoiceManagers;
using Models.Networks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class PitchSetting_FullScreen : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.FullScreen;
        private float resultPitch = -1f;

        [SerializeField] private PitchAnalyzer pitchAnalyzer;
        [SerializeField] private TextMeshProUGUI txt_recordBtn;
        [SerializeField] private TextMeshProUGUI txt_result;
        [SerializeField] private Image gaugeBar;
        

        private async void OnEnable() {
            if (SoundManager.Instance != null) {
                SoundManager.Instance.OnMicVolumeChanged += UpdateGauge;
                
                gaugeBar.fillAmount = 0f; 
            }
            // 1. 로컬 데이터 매니저에서 유저 ID 추출
            string myUserId = Managers.LocalDataManagers.LocalDataManager.Instance.userId;

            if (string.IsNullOrEmpty(myUserId)) {
                Debug.LogError("[PitchResultUI] 유저 ID가 비어있어 피치 데이터를 요청할 수 없습니다.");
                return;
            }

            // 2. 서버에서 디폴트 피치 비동기 조회 
            float serverPitch = await WebServerModel.Instance.GetDefaultPitchAsync(myUserId);

            // 3. 서버 응답 결과에 따른 UI 업데이트 처리
            if (serverPitch > 0f) {
                UpdateResultUI(serverPitch);
            }
        }
        
        private void OnDisable() {
            if (SoundManager.Instance != null) {
                SoundManager.Instance.OnMicVolumeChanged -= UpdateGauge;
            }
        }

        private void UpdateGauge(float volumeValue) {
            gaugeBar.fillAmount = volumeValue;
        }
        
        public void UpdateResultUI(float pitch) {
            resultPitch = pitch;
            txt_result.text = $"{resultPitch:F2} Hz";
        }



        public void OnClick_Record() {
            pitchAnalyzer.ToggleRecording();
            if (SoundManager.Instance != null)
            {
                
                if (SoundManager.Instance.isRecording)
                {
                    // 녹음이 켜졌으면 게이지를 0으로 초기화하고 텍스트도 변경
                    gaugeBar.fillAmount = 0f;
                    txt_recordBtn.text = "녹음중...";
                }
                else
                {
                    // 녹음이 꺼졌으면 확실하게 게이지를 0으로 닫아줍니다.
                    gaugeBar.fillAmount = 0f;
                    txt_recordBtn.text = "녹음";
                }
            }
        }

        public async void OnClick_Apply() {
            CommonUIController.Instance.GoBackToPreviousFullScreen();
        }
    }
}
