using System;
using DG.Tweening;
using UnityEngine;

namespace DefaultNamespace
{
    public class Option_Popup : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        [SerializeField] private GameObject lobbyUI;
        [SerializeField] private GameObject ingameUI;

        private CanvasGroup canvasGroup;
        private RectTransform popupRect;

        [Header("Animation Settings")]
        [SerializeField] private float animDuration = 0.25f; // 옵션창은 보통 더 빠르고 경쾌하게 띄움
        [SerializeField] private Vector3 startScale = new Vector3(0.8f, 0.8f, 0.8f);
        
        private bool isLobby = true;
        

        private void Awake() {
            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
        }


        public void CloseUI() {
            CloseAction();
        }

        public void SurrenderPressed() {
            Debug.Log("[Option_Lobby] Surrender Pressed");
        }

        public void VoiceSettingPressed() {
            Debug.Log("[Option_Lobby] Voice Setting Pressed");
        }
        
        public void TutorialPressed() {
            Debug.Log("[Option_Lobby] Tutorial Pressed");
        }
        
        public void LogoutPressed() {
            Debug.Log("[Option_Lobby] Logout Pressed");
        }
        
        public void ExitGamePressed() {
            Debug.Log("[Option_Lobby] Exit Game Pressed");
        }


        private void OnEnable() {
            lobbyUI.SetActive(isLobby);
            ingameUI.SetActive(!isLobby);
            
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
                UILoader.Instance.HideUI("Option_Lobby_Popup");
            });
        }

    }
}
