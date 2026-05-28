using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;

namespace DefaultNamespace
{
    public class DeckList_Room_Popup : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private Transform contentParent; // Vertical Layout Group이 있는 부모 객체
        [SerializeField] private DeckListPiece_Room deckItemPrefab; // 회색 슬롯 프리팹

        // Animation 용
        private CanvasGroup canvasGroup;
        private RectTransform popupRect;
        private float animDuration = 0.1f;
        private float moveOffsetX = 50f;
        [SerializeField] private float targetOffsetX = 200f;
        
        // 오브젝트 풀
        private IObjectPool<DeckListPiece_Room> deckPool;
        private List<DeckListPiece_Room> activeItems = new List<DeckListPiece_Room>();

        private void Awake()
        {
            // 풀링 초기화
            deckPool = new ObjectPool<DeckListPiece_Room>(
                createFunc: () => Instantiate(deckItemPrefab, contentParent),
                actionOnGet: (item) => item.gameObject.SetActive(true),
                actionOnRelease: (item) => item.gameObject.SetActive(false),
                actionOnDestroy: (item) => Destroy(item.gameObject)
            );
            
            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
        }
        
        // 🌟 추가: 열릴 때의 애니메이션
        public void ShowPopup()
        {
            // 1. 연타로 인한 트윈 겹침(버그) 방지
            popupRect.DOKill();
            canvasGroup.DOKill();

// 1. 시작 위치 세팅
            popupRect.anchoredPosition = new Vector2(targetOffsetX - moveOffsetX, popupRect.anchoredPosition.y);
            popupRect.localScale = Vector3.one * 0.8f;
            canvasGroup.alpha = 0f;

            // 3. 목표 상태로 애니메이션 진행 (제자리로, 원래 크기로, 불투명하게)
            // SetEase(Ease.OutBack)을 쓰면 도착할 때 살짝 튕기는 텐션감을 줍니다.
            popupRect.DOAnchorPosX(targetOffsetX, animDuration).SetEase(Ease.OutBack);
            popupRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutBack);
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.Linear);
        }

        // 🌟 추가: 닫힐 때의 애니메이션
        public void HidePopup()
        {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 목표 상태로 애니메이션 진행 (오른쪽에서 왼쪽으로, 작아지게, 투명하게)
            popupRect.DOAnchorPosX(targetOffsetX-moveOffsetX, animDuration).SetEase(Ease.InBack);
            popupRect.DOScale(Vector3.one * 0.8f, animDuration).SetEase(Ease.InBack);
        
            // 투명해지는 애니메이션이 완전히 끝난 후(OnComplete)에 SetActive(false) 처리
            canvasGroup.DOFade(0f, animDuration).SetEase(Ease.Linear).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }

        // 외부에서 덱 리스트 데이터를 넘겨주며 호출할 함수
        public void UpdateDeckListUI(List<DeckMetaData> myDecks)
        {
            // 1. 기존에 켜져 있던 아이템들 풀로 반환 (초기화)
            foreach (var item in activeItems)
            {
                deckPool.Release(item);
            }
            activeItems.Clear();

            // 2. 새로운 덱 데이터 개수만큼 UI 생성 및 설정
            foreach (var deck in myDecks)
            {
                DeckListPiece_Room piece = deckPool.Get();
                
                piece.Setup( deck ); 
            
                activeItems.Add(piece);
            }
        
            // LayoutGroup과 ContentSizeFitter가 프레임 끝에 크기를 갱신하므로 즉시 반영하려면 아래 코드 주석 해제
            // UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent.GetComponent<RectTransform>());
        }
    }


    public enum DeckElement {
        Fire,
        Water,
        Earth,
        Thunder,
        Wind,
        Ice,
        Vision,
        Life,
        Void,
        Normal
    }
    
    // Deck 데이터 클래스
    public class DeckMetaData
    {
        public string Name;
        public string CardCount;
        public DeckElement Element;
        public string DeckCode;
    }
}
