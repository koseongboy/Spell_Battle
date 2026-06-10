using Cards.CardUIDatas;
using DG.Tweening;
using Models.CardDatabases;
using TMPro;
using UnityEngine;

namespace DefaultNamespace {
    public class SpellActive_FullScreen : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<(string, Property)> {
        public EUILayer TargetLayer => EUILayer.Top;

        public TextMeshProUGUI txt_Spell;
        public RectTransform rect_Spell;
        public CanvasGroup canvasGroup;
        
        public void ReceiveData((string, Property) data) {
            Color dbColor = CardDatabase.Instance.GetElementData(data.Item2).Color;
            
            // 1. DB에서 가져온 색상이 투명할 경우를 대비해 알파값 강제 복구
            dbColor.a = 1f; 
            
            txt_Spell.color = dbColor;
            txt_Spell.text = data.Item1;
        }

        private void OnEnable() {
            StartAction();
        }

        private void OnDisable() {
            rect_Spell.DOKill();
            
            // 2. txt_Spell이 아닌 실제 애니메이션이 들어가는 canvasGroup을 Kill 해야 함
            canvasGroup.DOKill(); 
        }

        private void StartAction() {
            rect_Spell.DOKill();
            canvasGroup.DOKill();

            // 초기 상태 세팅 (5배 크기, 투명함)
            rect_Spell.localScale = Vector3.one * 5f;
            canvasGroup.alpha = 0f;

            // 3. 게임이 일시정지(TimeScale=0)되어도 애니메이션이 재생되도록 SetUpdate(true) 추가
            rect_Spell.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBounce).SetUpdate(true);
            canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuint).SetUpdate(true);
        }
    }
}