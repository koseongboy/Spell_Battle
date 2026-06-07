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

        public void OnClick_Apply() {
            if (!Mathf.Approximately(resultPitch, -1f)) {
                // TODO : 보이스 피치값 서버에 보내기
            }
            
            CommonUIController.Instance.GoBackToPreviousFullScreen();
        }
    }
}
