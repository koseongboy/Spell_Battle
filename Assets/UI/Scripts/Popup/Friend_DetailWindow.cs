using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
public class Friend_DetailWindow : MonoBehaviour, UI_ILayerInfo, UI_Popup, UI_IDataReceiver<FriendDataForUI> 
    {
        public EUILayer TargetLayer => EUILayer.Popup;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI txt_Tier;    // 좌측 상단 (예: 4)
        [SerializeField] private TextMeshProUGUI txt_Score;   // 좌측 하단 (예: 1892)
        [SerializeField] private TextMeshProUGUI txt_Name;    // 우측 상단 (예: Crocobob)
        [SerializeField] private TextMeshProUGUI txt_Status;  // 우측 하단 (예: 접속 중)
        
        [Header("Buttons")]
        [SerializeField] private Button btn_Close;
        [SerializeField] private Button btn_InviteGame; // 게임 초대
        [SerializeField] private Button btn_DeleteFriend; // 친구 삭제

        // 내부 캐싱용
        private int currentUserId;

        [Header("DOTween Settings")]
        private CanvasGroup canvasGroup;
        private RectTransform popupRect;
        private Vector2 originalPos;
        [SerializeField] private float moveOffsetX = 100f;
        [SerializeField] private float animDuration = 0.2f;
        [SerializeField] private Vector3 startScale = new Vector3(0.8f, 0.8f, 0.8f);

        private void Awake() 
        {
            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
            originalPos = popupRect.anchoredPosition;

            SetupEvents();
        }

        private void SetupEvents()
        {
            // 닫기 버튼은 컨트롤러의 닫기 함수 호출
            btn_Close.onClick.AddListener(() => {
                FriendPopupController.Instance.CloseDetailWindow();
            });
            
            // 초대/삭제 버튼은 컨트롤러에 현재 유저 ID를 담아서 전달
            btn_InviteGame.onClick.AddListener(() => {
                FriendPopupController.Instance.RequestInviteGame(currentUserId);
            });
            
            btn_DeleteFriend.onClick.AddListener(() => {
                FriendPopupController.Instance.RequestDeleteFriend(currentUserId);
            });
        }

        // 🌟 UILoader의 SendDataToUI에 의해 자동으로 호출되는 함수입니다!
        public void ReceiveData(FriendDataForUI data)
        {
            currentUserId = data.userId;
            
            txt_Tier.text = data.tier.ToString();
            txt_Score.text = data.score.ToString();
            txt_Name.text = data.name;
            
            // Enum 상태를 한글 텍스트로 변환
            txt_Status.text = data.onlineStatus switch {
                OnlineStatus.Online => "접속 중",
                OnlineStatus.Away => "자리 비움",
                OnlineStatus.Offline => "오프라인",
                _ => "상태 알 수 없음"
            };

            // 만약 오프라인이라면 게임 초대 버튼을 비활성화하는 디테일
            btn_InviteGame.interactable = (data.onlineStatus == OnlineStatus.Online);
        }

        #region UI_Popup Implementation (DOTween)

        public void OpenAction() 
        {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 🌟 초기 상태: 원래 위치보다 왼쪽에서 시작, 작아진 크기, 투명함
            popupRect.anchoredPosition = new Vector2(originalPos.x - moveOffsetX, originalPos.y);
            popupRect.localScale = startScale;
            canvasGroup.alpha = 0f;

            // 🌟 목표 상태로 애니메이션: 제자리(오른쪽)로 이동, 원래 크기로, 불투명하게
            popupRect.DOAnchorPosX(originalPos.x, animDuration).SetEase(Ease.OutQuint);
            popupRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuint);
        }

        public void CloseAction(Action onAnimationComplete) 
        {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 🌟 목표 상태로 애니메이션: 왼쪽으로 이동하면서, 작아지고, 투명하게
            popupRect.DOAnchorPosX(originalPos.x - moveOffsetX, animDuration).SetEase(Ease.InQuint);
            popupRect.DOScale(startScale, animDuration).SetEase(Ease.InQuint);
    
            canvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuint)
                .OnComplete(() => onAnimationComplete?.Invoke());
        }

        #endregion
    }
}
