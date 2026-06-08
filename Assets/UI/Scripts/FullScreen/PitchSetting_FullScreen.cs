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
        [SerializeField] private TextMeshProUGUI txt_result;
        [SerializeField] private Image gaugeBar;
        

        private async void OnEnable() {
            if (VoiceManager.Instance != null) {
                VoiceManager.Instance.OnMicVolumeChanged += UpdateGauge;
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
            if (VoiceManager.Instance != null) {
                VoiceManager.Instance.OnMicVolumeChanged -= UpdateGauge;
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
            if (VoiceManager.Instance != null)
            {
                if (VoiceManager.Instance.isRecording)
                {
                    // 녹음이 켜졌으면 게이지를 0으로 초기화하고 텍스트도 변경
                    gaugeBar.fillAmount = 0f;
                    //btnText.text = "녹음 중지";
                }
                else
                {
                    // 녹음이 꺼졌으면 확실하게 게이지를 0으로 닫아줍니다.
                    gaugeBar.fillAmount = 0f;
                    //btnText.text = "녹음 시작";
                }
            }
        }

        public async void OnClick_Apply() {
            if (!Mathf.Approximately(resultPitch, -1f)) {
                CommonUIController.Instance.ShowLoading();
                var serverSuccess = await pitchAnalyzer.SendServerPitch( resultPitch );

                if (serverSuccess) {
                    CommonUIController.Instance.DoneLoading();
                    CommonUIController.Instance.GoBackToPreviousFullScreen();
                    CommonUIController.Instance.ShowBlackAlert("보이스 세팅이 저장되었습니다.");
                }
                else {
                    CommonUIController.Instance.DoneLoading();
                    CommonUIController.Instance.ShowRedAlert("서버 통신에 실패했습니다. 보이스 세팅을 다시 진행해주세요.");
                }
            }
            CommonUIController.Instance.GoBackToPreviousFullScreen();
        }
    }
}
