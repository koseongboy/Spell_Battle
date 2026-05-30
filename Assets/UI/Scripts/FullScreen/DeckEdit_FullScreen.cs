using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Cards.CardUIDatas;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace DefaultNamespace
{
    public class DeckEdit_FullScreen : MonoBehaviour {
        [Header("Transforms (Layout Groups)")] public Transform centerCardContent;
        public Transform leftDeckListContent;
        public Transform rightCardInDeckContent;

        [Header("Prefabs")] public UI_Card uiCardPrefab;
        public DeckListPiece deckListPiecePrefab;
        public CardInDeckPiece cardInDeckPiecePrefab;

        [Header("Pagination Arrows")]
        public Button prevPageButton;
        public Button nextPageButton;

        [Header("Top & Bottom Filters")] 
        public Transform propertyFilterContainer;
        public Transform costFilterContainer;

        // 인스펙터에서 숨기고, 코드에서 자동으로 채웁니다.
        [HideInInspector] public UI_PropertyButton[] propertyFilters;
        [HideInInspector] public CostInDeckEdit[] costFilters;

        [Header("Deck Controls")] public Button saveButton;
        public Button clearButton;
        public TextMeshProUGUI deckCountText;


        // --- 🌟 3개의 Object Pool ---
        private IObjectPool<UI_Card> cardPool;
        private IObjectPool<DeckListPiece> deckListPool;
        private IObjectPool<CardInDeckPiece> cardInDeckPool;

        // 활성화된 객체 추적 리스트
        private List<UI_Card> activeCards = new List<UI_Card>();
        private List<DeckListPiece> activeDeckLists = new List<DeckListPiece>();
        private List<CardInDeckPiece> activeCardsInDeck = new List<CardInDeckPiece>();

        private void Awake() {
            propertyFilters = propertyFilterContainer.GetComponentsInChildren<UI_PropertyButton>();
            costFilters = costFilterContainer.GetComponentsInChildren<CostInDeckEdit>(); // 🌟 자동 수집

            // 1. 중앙 카드 풀
            cardPool = new ObjectPool<UI_Card>(
                () => Instantiate(uiCardPrefab, centerCardContent),
                (obj) => obj.gameObject.SetActive(true),
                (obj) => obj.gameObject.SetActive(false),
                (obj) => Destroy(obj.gameObject),
                true, 8, 20
            );

            // 2. 좌측 덱 리스트 풀
            deckListPool = new ObjectPool<DeckListPiece>(
                () => Instantiate(deckListPiecePrefab, leftDeckListContent),
                (obj) => obj.gameObject.SetActive(true),
                (obj) => obj.gameObject.SetActive(false),
                (obj) => Destroy(obj.gameObject),
                true, 5, 10
            );

            // 3. 우측 덱 속 카드 리스트 풀
            cardInDeckPool = new ObjectPool<CardInDeckPiece>(
                () => Instantiate(cardInDeckPiecePrefab, rightCardInDeckContent),
                (obj) => obj.gameObject.SetActive(true),
                (obj) => obj.gameObject.SetActive(false),
                (obj) => Destroy(obj.gameObject),
                true, 20, 50 // 덱 최대 사이즈 고려
            );
        }

        private void OnEnable() {
            DeckEditController controller = FindObjectOfType<DeckEditController>();
            if (controller != null) controller.RegisterView(this);
        }

        // ==========================================
        // Controller에서 호출할 [Get] 함수들
        // ==========================================
        public UI_Card GetCardFromPool() {
            var obj = cardPool.Get();
            activeCards.Add(obj);
            return obj;
        }

        public DeckListPiece GetDeckListFromPool() {
            var obj = deckListPool.Get();
            activeDeckLists.Add(obj);
            return obj;
        }

        public CardInDeckPiece GetCardInDeckFromPool() {
            var obj = cardInDeckPool.Get();
            activeCardsInDeck.Add(obj);
            return obj;
        }

        // ==========================================
        // 기존 ClearContainer를 대체하는 [Return] 함수들
        // ==========================================
        public void ReturnAllCardsToPool() {
            foreach (var obj in activeCards) cardPool.Release(obj);
            activeCards.Clear();
        }

        public void ReturnAllDeckListsToPool() {
            foreach (var obj in activeDeckLists) deckListPool.Release(obj);
            activeDeckLists.Clear();
        }

        public void ReturnAllCardsInDeckToPool() {
            foreach (var obj in activeCardsInDeck) cardInDeckPool.Release(obj);
            activeCardsInDeck.Clear();
        }
    }
}
