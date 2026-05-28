using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace
{
// 인터페이스명에 맞게 UI_Popup 상속
    public class Friend_RequestWindow : MonoBehaviour, UI_ILayerInfo, UI_Popup, UI_IDataReceiver<List<FriendDataForUI>>
    {
        public EUILayer TargetLayer => EUILayer.Popup;

        [Header("UI Elements")]
        [SerializeField] private Button btn_Close;
        [SerializeField] private Transform requestListContent; // 스크롤 뷰 Content
        [FormerlySerializedAs("requestItemPrefab")] [SerializeField] private FriendRequestPiece requestPiecePrefab;

        // Controller로 보낼 이벤트
        public Action OnClick_Close;
        public Action<int> OnClick_Accept;
        public Action<int> OnClick_Reject;

        // 풀링 관련 변수
        private IObjectPool<FriendRequestPiece> requestPool;
        private List<FriendRequestPiece> activeRequestItems = new List<FriendRequestPiece>();

        [Header("Movement & DOTween Settings")]
        private CanvasGroup canvasGroup;
        private RectTransform popupRect;
        private Vector2 originalPos;
        [SerializeField] private float moveOffsetX = 100f; // 🌟 좌우 이동 거리
        [SerializeField] private float animDuration = 0.25f;
        [SerializeField] private Vector3 startScale = new Vector3(0.8f, 0.8f, 0.8f);

        private void Awake() 
        {
            if (FriendPopupController.Instance != null) {
                FriendPopupController.Instance.RegisterRequestWindow(this);
            }

            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
            originalPos = popupRect.anchoredPosition;

            btn_Close.onClick.AddListener(() => OnClick_Close?.Invoke());

            requestPool = new ObjectPool<FriendRequestPiece>(
                createFunc: CreateRequestItem,
                actionOnGet: (item) => item.gameObject.SetActive(true),
                actionOnRelease: (item) => item.gameObject.SetActive(false),
                actionOnDestroy: (item) => Destroy(item.gameObject),
                defaultCapacity: 10,
                maxSize: 30
            );
        }

        private FriendRequestPiece CreateRequestItem()
        {
            FriendRequestPiece piece = Instantiate(requestPiecePrefab, requestListContent);
            // 수락/거절 이벤트 연결
            piece.Init(
                (userId) => OnClick_Accept?.Invoke(userId),
                (userId) => OnClick_Reject?.Invoke(userId)
            );
            return piece;
        }

        // Controller가 받은 요청 목록을 그리는 함수
        public void UpdateUI_RequestList(List<FriendDataForUI> requestList)
        {
            foreach (FriendRequestPiece item in activeRequestItems) {
                requestPool.Release(item);
            }
            activeRequestItems.Clear();

            foreach (var data in requestList) {
                FriendRequestPiece piece = requestPool.Get();
                piece.SetData(data);
                activeRequestItems.Add(piece);
            }
        }

        public void ReceiveData(List<FriendDataForUI> data) {
            UpdateUI_RequestList(data);
        }

        #region UI_Popup Implementation (변경된 함수명 적용)

        public void OpenAction() 
        {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 왼쪽에서 시작
            popupRect.anchoredPosition = new Vector2(originalPos.x - moveOffsetX, originalPos.y);
            popupRect.localScale = startScale;
            canvasGroup.alpha = 0f;

            // 오른쪽(제자리)으로 OutQuint 부드럽게 이동
            popupRect.DOAnchorPosX(originalPos.x, animDuration).SetEase(Ease.OutQuint);
            popupRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.Linear);
        }

        public void CloseAction(Action onAnimationComplete) 
        {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 왼쪽으로 InQuint 이동하며 사라짐
            popupRect.DOAnchorPosX(originalPos.x - moveOffsetX, animDuration).SetEase(Ease.InQuint);
            popupRect.DOScale(startScale, animDuration).SetEase(Ease.InQuint);
            
            canvasGroup.DOFade(0f, animDuration).SetEase(Ease.Linear)
                .OnComplete(() => onAnimationComplete?.Invoke());
        }

        #endregion
    }
}
