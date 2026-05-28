using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class Friend_SearchWindow : MonoBehaviour, UI_ILayerInfo, UI_Popup
    {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        [Header("Search UI Elements")]
        [SerializeField] private TMP_InputField input_Search;
        [SerializeField] private Button btn_SearchIcon;

        [Header("Search Result Elements (ScrollView)")]
        [SerializeField] private Transform searchResultContent; // 스크롤 뷰의 Content
        [SerializeField] private FriendSearchPiece searchItemPrefab; // 위에서 만든 프리팹
        // 풀링 관련 변수
        private IObjectPool<FriendSearchPiece> searchPool;
        private List<FriendSearchPiece> activeSearchItems = new List<FriendSearchPiece>();
        
        
        // Controller로 보낼 이벤트들
        public Action OnClick_Close;
        public Action<string> OnSubmit_Search;
        public Action<int> OnClick_AddFriend;

        private int currentResultUserId; // 현재 검색된 유저의 ID 캐싱
        
        
        [Header("DOTween Settings")]
        private CanvasGroup canvasGroup;
        private RectTransform popupRect;
        private Vector2 originalPos;
        [SerializeField] private float moveOffsetX = 100f;
        [SerializeField] private float animDuration = 0.2f;
        [SerializeField] private Vector3 startScale = new Vector3(0.8f, 0.8f, 0.8f);
        
        private void Awake() 
        {
            // 🌟 1. 깨어나자마자 Controller에 자신을 등록
            if (FriendPopupController.Instance != null) {
                FriendPopupController.Instance.RegisterSearchWindow(this);
            }

            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
            originalPos = popupRect.anchoredPosition;

            SetupEvents();
            
            // 오브젝트 풀 초기화
            searchPool = new ObjectPool<FriendSearchPiece>(
                createFunc: () => { 
                    FriendSearchPiece piece = Instantiate(searchItemPrefab, searchResultContent);
                    // [+] 버튼을 눌렀을 때 Controller까지 전달되도록 이벤트 연결
                    piece.Init((userId) => OnClick_AddFriend?.Invoke(userId));
                    return piece;
                },
                actionOnGet: (item) => item.gameObject.SetActive(true),
                actionOnRelease: (item) => item.gameObject.SetActive(false),
                actionOnDestroy: (item) => Destroy(item.gameObject),
                defaultCapacity: 5,
                maxSize: 20
            );
        }

        private void SetupEvents()
        {
            // 엔터 키를 쳤을 때 검색 실행
            input_Search.onSubmit.AddListener((text) => 
            {
                if (!string.IsNullOrWhiteSpace(text)) OnSubmit_Search?.Invoke(text);
            });

            // 돋보기 버튼 클릭 시 검색 실행
            if(btn_SearchIcon != null) {
                btn_SearchIcon.onClick.AddListener(() => {
                    if (!string.IsNullOrWhiteSpace(input_Search.text)) OnSubmit_Search?.Invoke(input_Search.text);
                });
            }


        }
        
        public void ShowSearchResult(List<FriendDataForUI> resultList)
        {
            ClearSearchResult(); // 기존 리스트 밀어버리기

            foreach (var data in resultList)
            {
                FriendSearchPiece item = searchPool.Get();
                item.SetData(data);
                activeSearchItems.Add(item);
            }
        }

        // Controller가 검색 실패 시 호출하는 함수
        public void ClearSearchResult()
        {
            foreach (FriendSearchPiece piece in activeSearchItems)
            {
                searchPool.Release(piece);
            }
            activeSearchItems.Clear();
        }

        #region UI Action

        public void OpenAction() 
        {
            // 창이 열릴 때 이전 검색 기록 초기화
            input_Search.text = "";
            ClearSearchResult();

            popupRect.DOKill();
            canvasGroup.DOKill();

            popupRect.anchoredPosition = new Vector2(originalPos.x - moveOffsetX, originalPos.y);
            popupRect.localScale = startScale;
            canvasGroup.alpha = 0f;

            // 🌟 목표 상태로 애니메이션: 원래 위치로 이동(우측 이동), 원래 크기로, 불투명하게
            popupRect.DOAnchorPosX(originalPos.x, animDuration).SetEase(Ease.OutQuint);
            popupRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuint);
        }

        public void CloseAction(Action onAnimationComplete) 
        {
            popupRect.DOKill();
            canvasGroup.DOKill();

            popupRect.DOAnchorPosX(originalPos.x - moveOffsetX, animDuration).SetEase(Ease.InQuint);
            popupRect.DOScale(startScale, animDuration).SetEase(Ease.InQuint);
            canvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuint)
                .OnComplete(() => onAnimationComplete?.Invoke());
        }

        #endregion
    }
}
