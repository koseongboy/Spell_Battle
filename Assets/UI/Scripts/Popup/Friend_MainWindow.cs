using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class Friend_MainWindow : MonoBehaviour, UI_ILayerInfo, UI_Popup {
        public EUILayer TargetLayer => EUILayer.Popup;

        
        
        [Header("UI Element")]
        [SerializeField] private TextMeshProUGUI txt_Tier;
        [SerializeField] private TextMeshProUGUI txt_Score;
        [SerializeField] private TextMeshProUGUI txt_Name;
        
        [Header("UI Buttons")]
        [SerializeField] private Button btn_AddFriend;
        [SerializeField] private Button btn_Alert;
        
        [Header("Friend List Element")]
        [SerializeField] private Transform friendListContent; // 🌟 추가: 스크롤 뷰의 Content Transform
        [SerializeField] private FriendPanelPiece friendItemPrefab;
        
        public Action OnClick_AddFriend;
        public Action OnClick_Alert;
        public Action<int> OnClick_FriendDetail;
        
        private IObjectPool<FriendPanelPiece> friendPool;
        private List<FriendPanelPiece> activeFriendItems = new List<FriendPanelPiece>();
        
        [Header("For DOTween")]
        private CanvasGroup canvasGroup;
        private RectTransform popupRect;

        [Header("Animation Settings")]
        private Vector2 originalPos;
        [SerializeField] private float animDuration = 0.1f;
        [SerializeField] private float startOffsetY = 300f; // 시작 시 위로 얼마나 올라가 있을지
        [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);
        


        private void Awake() {
            FriendPopupController.Instance.RegisterMainWindow(this);

            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
            originalPos = popupRect.anchoredPosition;
            
            SetupButtonEvent();
            
            // 오브젝트 풀 초기화 설정
            friendPool = new ObjectPool<FriendPanelPiece>(
                createFunc: CreateFriendItem,
                actionOnGet: (item) => item.gameObject.SetActive(true),
                actionOnRelease: (item) => item.gameObject.SetActive(false),
                actionOnDestroy: (item) => Destroy(item.gameObject),
                defaultCapacity: 10,
                maxSize: 50
            );
        }

        private void OnEnable() {
            FriendPopupController.Instance.UpdateUI_MainWindow();
        }
        

        // 풀에 아이템이 부족할 때 새로 찍어내는 공장 함수
        private FriendPanelPiece CreateFriendItem()
        {
            FriendPanelPiece piece = Instantiate(friendItemPrefab, friendListContent);
            // 아이템이 생성될 때 Controller까지 이어지는 클릭 이벤트를 주입
            piece.Init((userId) => OnClick_FriendDetail?.Invoke(userId));
            return piece;
        }

        private void SetupButtonEvent() {
            btn_AddFriend.onClick.AddListener(() => OnClick_AddFriend?.Invoke());
            btn_Alert.onClick.AddListener(() => OnClick_Alert?.Invoke());
        }
        
        public void UpdateUI((int,int,string) profileData, List<FriendDataForUI> fDataList) 
        {
            UpdateUI_MyProfile(profileData);
            UpdateUI_FriendList(fDataList);
            OpenAction(); // 데이터 세팅 후 애니메이션 실행
        }

        private void UpdateUI_MyProfile((int tier, int score, string name) profileData) {
            txt_Tier.text = profileData.tier.ToString();
            txt_Score.text = profileData.score.ToString();
            txt_Name.text = profileData.name;
        }
        
        private void UpdateUI_FriendList(List<FriendDataForUI> fDataList) 
        {
            // 1. 기존에 켜져 있던 아이템들을 모두 풀(Pool)로 반납 (에러 수정된 부분)
            foreach (FriendPanelPiece item in activeFriendItems)
            {
                friendPool.Release(item); // 이제 타입이 정확히 일치하여 에러가 나지 않습니다.
            }
            activeFriendItems.Clear(); // 반납 후 리스트 비우기

            // 2. 새로운 리스트 생성
            foreach (var friendData in fDataList)
            {
                FriendPanelPiece piece = friendPool.Get(); // 없으면 새로 만들고, 있으면 재활용
                piece.SetData(friendData);
                activeFriendItems.Add(piece);
            }
        }


        public void OpenAction() {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 1. 초기 상태 강제 세팅 (위로 이동, 작아진 크기, 투명함)
            popupRect.anchoredPosition = new Vector2(originalPos.x, originalPos.y + startOffsetY);
            popupRect.localScale = startScale;
            canvasGroup.alpha = 0f;

            // 2. 목표 상태로 애니메이션 진행 (제자리로, 원래 크기로, 불투명하게)
            popupRect.DOAnchorPosY(originalPos.y, animDuration).SetEase(Ease.OutQuint);
            popupRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.Linear);
        }

        public void CloseAction(Action onAnimationComplete) {
            popupRect.DOKill();
            canvasGroup.DOKill();

            popupRect.DOAnchorPosY(originalPos.y + startOffsetY, animDuration).SetEase(Ease.InQuint);
            popupRect.DOScale(startScale, animDuration).SetEase(Ease.InQuint);
        
            canvasGroup.DOFade(0f, animDuration).SetEase(Ease.Linear).OnComplete(() =>
            {
                onAnimationComplete?.Invoke();
            });
        }
    }

}
