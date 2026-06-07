using System.Collections;
using Managers.VoiceManagers;
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
        
        private Coroutine micCoroutine;

        private void OnEnable() {
            resultPitch = -1f;
            
            // TODO : 서버에서 내 값 가져와서 UpdateResultUI
        }

        public void UpdateResultUI(float pitch) {
            resultPitch = pitch;
            
            txt_result.text = $"{resultPitch:F2} Hz";
        }


        public void StartGaugeCoroutine() {
            // TODO
            return;
            micCoroutine = StartCoroutine(MicTestRoutine());
        }

        public void StopGaugeCoroutine() {
            // TODO
            return;
            
            if (micCoroutine == null) return;
            
            StopCoroutine(micCoroutine);
            micCoroutine = null;
        }
        
                
        public IEnumerator MicTestRoutine()
        {
            // TODO : 마이크 게인 따라서 image 바꿔주기
            
            // // isRecording이 true인 동안에만 매 프레임 게이지를 갱신합니다.
            // while (VoiceManager.Instance != null && VoiceManager.Instance.isRecording)
            // {
            //     gaugeBar.fillAmount = VoiceManager.Instance.GetMicVolumeGauge();
            //     Debug.Log(VoiceManager.Instance.GetMicVolumeGauge());
            //     yield return null; // 1프레임 대기
            // }

            yield return null;
        }

        public void OnClick_Record() {
            pitchAnalyzer.ToggleRecording();
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
