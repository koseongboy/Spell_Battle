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
            dbColor.a = 1f;
            
            txt_Spell.color = dbColor;
            txt_Spell.text = data.Item1;
        }

        private void OnEnable() {
            StartAction();
        }

        private void OnDisable() {
            rect_Spell.DOKill();
            canvasGroup.DOKill();
        }

        private void StartAction() {
            // TODO : 아 좀 더 멋있는 연출 넣어야하는데
            
            rect_Spell.DOKill();
            canvasGroup.DOKill();

            // 1. 초기 상태 세팅 (5배 크기, 투명함)
            rect_Spell.localScale = Vector3.one * 5f;
            canvasGroup.alpha = 0f;

            rect_Spell.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBounce);
            canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuint);
        }
    }
}