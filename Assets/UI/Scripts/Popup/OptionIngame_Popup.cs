using System;
using DG.Tweening;
using UnityEngine;

namespace DefaultNamespace
{
    public class OptionIngame_Popup : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        private CanvasGroup canvasGroup;
        private RectTransform popupRect;

        [Header("Animation Settings")]
        [SerializeField] private float animDuration = 0.25f; // 옵션창은 보통 더 빠르고 경쾌하게 띄움
        [SerializeField] private Vector3 startScale = new Vector3(0.8f, 0.8f, 0.8f);
        

        private void Awake() {
            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
        }


        public void CloseUI() {
            CloseAction();
        }

        public void SurrenderPressed() {
            Debug.Log("[Option_Lobby] Surrender Pressed");
            // TODO : Confirm 창 띄우기
        }

        private void OnEnable() {
            OpenAction();
        }

        private void OpenAction() {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 1. 초기 상태 세팅 (이동 없음, 작아진 크기, 투명함)
            popupRect.localScale = startScale;
            canvasGroup.alpha = 0f;

            // 2. 목표 상태로 애니메이션 (원래 크기로, 불투명하게)
            popupRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuint);
        }

        private void CloseAction() {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 목표 상태로 애니메이션 (다시 작아지게, 투명하게)
            popupRect.DOScale(startScale, animDuration).SetEase(Ease.InQuint);
        
            canvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuint).OnComplete(() =>
            {
                UILoader.Instance.HideUI("Option_Ingame_Popup");
            });
        }

    }
}
