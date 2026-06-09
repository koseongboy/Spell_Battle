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

        [Header("Prefabs")] public UI_Card_DeckEdit uiCardDeckEditPrefab;
        [FormerlySerializedAs("deckListPiecePrefab")] public DeckPiece_DeckEdit deckPieceDeckEditPrefab;
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
        public Button btn_DeleteDeck;
        public Button btn_RenameDeck;
        
        [Header("New Deck UI")]
        public Button btn_NewDeck;
        public GameObject popup_NewDeck;
        public TMP_InputField input_NewDeckName;
        public Button btn_ConfirmNewDeck;
        public Button btn_CloseNewDeckPopup;
        
        [Header("Rename Deck UI")]
        public GameObject popup_RenameDeck;
        public TMP_InputField input_RenameDeckName;
        public Button btn_ConfirmRenameDeck;
        public Button btn_CloseRenameDeckPopup;

        [Header("Drop Zone")]
        // 카드를 놓았을 때 추가로 인정할 우측 영역
        public RectTransform dropZoneRect;

        // --- 3개의 Object Pool ---
        private IObjectPool<UI_Card_DeckEdit> cardPool;
        private IObjectPool<DeckPiece_DeckEdit> deckListPool;
        private IObjectPool<CardInDeckPiece> cardInDeckPool;

        // 활성화된 객체 추적 리스트
        private List<UI_Card_DeckEdit> activeCards = new List<UI_Card_DeckEdit>();
        private List<DeckPiece_DeckEdit> activeDeckLists = new List<DeckPiece_DeckEdit>();
        private List<CardInDeckPiece> activeCardsInDeck = new List<CardInDeckPiece>();

        private void Awake() {
            propertyFilters = propertyFilterContainer.GetComponentsInChildren<UI_PropertyButton>();
            costFilters = costFilterContainer.GetComponentsInChildren<CostInDeckEdit>(); // 🌟 자동 수집

            // 1. 중앙 카드 풀
            cardPool = new ObjectPool<UI_Card_DeckEdit>(
                () => Instantiate(uiCardDeckEditPrefab, centerCardContent),
                (obj) => obj.gameObject.SetActive(true),
                (obj) => obj.gameObject.SetActive(false),
                (obj) => Destroy(obj.gameObject),
                true, 8, 20
            );

            // 2. 좌측 덱 리스트 풀
            deckListPool = new ObjectPool<DeckPiece_DeckEdit>(
                () => Instantiate(deckPieceDeckEditPrefab, leftDeckListContent),
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
            
            if (popup_NewDeck != null) {
                popup_NewDeck.SetActive(false);
            }
            
            if (popup_RenameDeck != null) {
                popup_RenameDeck.SetActive(false);
            }
        }

        private void OnEnable() {
            DeckEditController controller = FindObjectOfType<DeckEditController>();
            if (controller != null) controller.RegisterView(this);
            
            if (LeftUpperController.Instance != null) {
                LeftUpperController.Instance.SetBackAction(() => {
                    CommonUIController.Instance.GoBackToPreviousFullScreen();
                });
            }
        }

        // ==========================================
        // Controller에서 호출할 [Get] 함수들
        // ==========================================
        public UI_Card_DeckEdit GetCardFromPool() {
            var obj = cardPool.Get();
            activeCards.Add(obj);
            return obj;
        }

        public DeckPiece_DeckEdit GetDeckListFromPool() {
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
