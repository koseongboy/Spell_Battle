using Controllers.SpellControllers;
using Models.Networks;
using TMPro;
using UnityEngine;

namespace DefaultNamespace
{
    public class SpellResult_FullScreen : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<TaskStatusResponse>
    {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        [SerializeField] private TextMeshProUGUI txt_sentense;
        [SerializeField] private TextMeshProUGUI txt_score;
        
        public void ReceiveData(TaskStatusResponse result) {
            txt_sentense.text = result.recognizedSentence;
            txt_score.text = result.score.ToString();
        }

        public void PlayRecord() {
            if (SpellController.Instance != null) 
            {
                SpellController.Instance.PlayRecordedAudio();
            }
            else 
            {
                Debug.LogError("[SpellResult_FullScreen] SpellController를 찾을 수 없습니다.");
            }
        }

        public void ApplySpell() {
            SpellController.Instance.SubmitConfirmedSpell();
        }

        public void Rollback() {
            // 그냥 현재 UI를 닫기. 그럼 뒤에 있는 Spell UI가 나옴.
            UILoader.Instance.HideUI("SpellResult_FullScreen");
        }
    }
}
