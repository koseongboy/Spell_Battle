using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cards.PlayableCards;
using Cards.CardUIDatas;
using Cards.EffectInfos;
using Managers.DataManagers;
using UnityEngine.Serialization;

namespace DefaultNamespace
{
    public class DeckEditController : MonoBehaviour
    {
        [SerializeField] private DeckEdit_FullScreen ui_DeckEdit;

        // --- 데이터 상태 ---
        private List<GenericCard> allCards = new List<GenericCard>();
        private List<GenericCard> currentFilteredCards = new List<GenericCard>();
        
        // TODO : 현재 편집 중인 덱 데이터 (실제로는 PlayerData 등의 모델과 연동)
        private string currentDeckName = "불의 세례를 받아라";
        private List<int> currentDeckCardIds = new List<int>(); 

        // 필터 상태 (-1이나 None이면 필터 꺼짐)
        private Property currentPropertyFilter = Property.None;
        private int currentCostFilter = -1; 
        
        // --- 페이징 상태 ---
        private int currentPage = 0;
        private const int CARDS_PER_PAGE = 8; // 중앙 윈도우 최대 표시 개수

        private const int MAX_DECK_SIZE = 45;
        private const int MAX_SAME_CARD = 3;

        private void Start()
        {
            // 1. 모든 카드 데이터 로드
            allCards = CardDataManager.Instance.GetAllCards();
        }
        
        // View가 OnEnable될 때 스스로 호출하는 함수
        public void RegisterView(DeckEdit_FullScreen newView)
        {
            ui_DeckEdit = newView;
            
            // 2. 버튼 이벤트 연동
            SetupFilterButtons();
            ui_DeckEdit.saveButton.onClick.AddListener(SaveDeck);
            ui_DeckEdit.clearButton.onClick.AddListener(ClearDeck);

            // 3. 최초 화면 갱신
            RefreshLeftDeckList();
            ApplyFilters(); // 필터 적용 후 페이징 연산 & 화면 갱신
            RefreshRightDeckCards();
        }


        private void SetupFilterButtons()
        {
            // 1. 속성 필터 연동 (View에 세팅된 리스트를 그대로 순회)
            foreach (var filterUI in ui_DeckEdit.propertyFilters)
            {
                Property p = filterUI.property;
            
                // 버튼 클릭 이벤트 바인딩
                filterUI.button.onClick.AddListener(() => TogglePropertyFilter(p));
            
                // 초기 상태는 모두 하이라이트 꺼짐
                if (filterUI.highlightObj != null)
                    filterUI.highlightObj.SetActive(false);
            }

            // 2. 코스트 필터 연동 (기존과 동일)
            for (int i = 0; i <= 10; i++)
            {
                int cost = i;
                ui_DeckEdit.BindCostFilter(i, () => ToggleCostFilter(cost));
            }
        }

        // ==========================================
        // 필터링 토글 로직
        // ==========================================
        private void TogglePropertyFilter(Property prop)
        {
            // 이미 켜진 속성을 다시 누르면 필터 해제(None)
            currentPropertyFilter = (currentPropertyFilter == prop) ? Property.None : prop;

            // 🌟 [UI 시각적 갱신] 선택된 버튼만 하이라이트 켜기
            foreach (var filterUI in ui_DeckEdit.propertyFilters)
            {
                if (filterUI.highlightObj != null)
                {
                    // 현재 누른 속성과 일치하면 True(켜짐), 아니면 False(꺼짐)
                    bool isSelected = (filterUI.property == currentPropertyFilter);
                    filterUI.highlightObj.SetActive(isSelected);
                }
            }

            ApplyFilters(); // 데이터 필터링 및 화면 갱신
        }

        private void ToggleCostFilter(int cost)
        {
            // 이미 켜진 코스트를 다시 누르면 필터 해제
            currentCostFilter = (currentCostFilter == cost) ? -1 : cost;
            RefreshCenterCards();
        }
        
        // 필터링 적용 및 페이지 리셋
        private void ApplyFilters()
        {
            currentFilteredCards = allCards.Where(c => 
                (currentPropertyFilter == Property.None || c.uiData.property == currentPropertyFilter) &&
                (currentCostFilter == -1 || c.uiData.cost == currentCostFilter || (currentCostFilter == 10 && c.uiData.cost >= 10))
            ).ToList();

            currentPage = 0; // 필터가 바뀌면 무조건 첫 페이지로 돌아감
            RefreshCenterCards();
        }
        
        // 페이지 변경
        private void ChangePage(int direction)
        {
            int maxPage = Mathf.Max(0, Mathf.CeilToInt((float)currentFilteredCards.Count / CARDS_PER_PAGE) - 1);
            currentPage = Mathf.Clamp(currentPage + direction, 0, maxPage);
            RefreshCenterCards();
        }

        // ==========================================
        // 화면 갱신 로직 (View 업데이트)
        // ==========================================
        private void RefreshCenterCards()
        {
            // 1. 기존에 표시된 카드들을 전부 풀(Pool)로 회수 (오브젝트 파괴 X)
            ui_DeckEdit.ReturnAllCardsToPool();

            // 2. 현재 페이지에 맞는 8개의 데이터만 슬라이싱(Skip & Take)
            int startIndex = currentPage * CARDS_PER_PAGE;
            var pageCards = currentFilteredCards.Skip(startIndex).Take(CARDS_PER_PAGE).ToList();

            // 3. 풀에서 카드를 꺼내 데이터 덮어씌우기
            foreach (var cardData in pageCards)
            {
                UI_Card cardObj = ui_DeckEdit.GetCardFromPool();
                cardObj.Init(cardData, OnCardClickedToAdd);
            }

            // 4. 화살표 표시/숨김 갱신
            UpdatePaginationUI();
        }
        
        // 더 이상 갈 곳이 없으면 화살표를 숨김
        private void UpdatePaginationUI()
        {
            int maxPage = Mathf.Max(0, Mathf.CeilToInt((float)currentFilteredCards.Count / CARDS_PER_PAGE) - 1);

            ui_DeckEdit.prevPageButton.gameObject.SetActive(currentPage > 0);
            ui_DeckEdit.nextPageButton.gameObject.SetActive(currentPage < maxPage);
        }


        private void RefreshRightDeckCards()
        {
            // AS-IS: view.ClearContainer(view.rightCardInDeckContainer);
            // TO-BE: 풀로 안전하게 회수
            ui_DeckEdit.ReturnAllCardsInDeckToPool();

            var groupedCards = currentDeckCardIds
                .GroupBy(id => id)
                .Select(group => new { 
                    Data = CardDataManager.Instance.GetCardById(group.Key), 
                    Count = group.Count() 
                })
                .OrderBy(c => c.Data.uiData.cost) 
                .ToList();

            foreach (var item in groupedCards)
            {
                CardInDeckPiece piece = ui_DeckEdit.GetCardInDeckFromPool();
                piece.Init(item.Data, item.Count, OnCardClickedToRemove);
            }

            ui_DeckEdit.deckCountText.text = $"{currentDeckCardIds.Count} / {MAX_DECK_SIZE}";
        }


        private void RefreshLeftDeckList()
        {
            // AS-IS: view.ClearContainer(view.leftDeckListContainer);
            ui_DeckEdit.ReturnAllDeckListsToPool();

            // TODO: 유저가 가진 여러 덱 리스트를 불러와서 반복문으로 생성해야 함.
            // 현재는 임시로 1개만 띄움
            DeckListPiece piece = ui_DeckEdit.GetDeckListFromPool();
            piece.Init(currentDeckName, true, (deckName) => { 
                currentDeckName = deckName;
                RefreshRightDeckCards(); 
            });
        }

        // ==========================================
        // 카드 추가 / 제거 로직
        // ==========================================
        private void OnCardClickedToAdd(GenericCard card)
        {
            if (currentDeckCardIds.Count >= MAX_DECK_SIZE)
            {
                CommonUIController.Instance.ShowRedAlert("덱에 카드를 더 추가할 수 없습니다.");
                return;
            }

            int currentCount = currentDeckCardIds.Count(id => id == card.uiData.id);
            if (currentCount >= MAX_SAME_CARD)
            {
                CommonUIController.Instance.ShowRedAlert($"동일한 카드는 {MAX_SAME_CARD}장까지만 넣을 수 있습니다!");
                return;
            }

            currentDeckCardIds.Add(card.uiData.id);
            RefreshRightDeckCards(); // 덱이 변했으니 우측 리스트 갱신
        }

        private void OnCardClickedToRemove(GenericCard card)
        {
            currentDeckCardIds.Remove(card.uiData.id); // 한 장만 제거
            RefreshRightDeckCards();
        }

        // ==========================================
        // 저장 및 비우기
        // ==========================================
        private void ClearDeck()
        {
            currentDeckCardIds.Clear();
            RefreshRightDeckCards();
        }

        private void SaveDeck()
        {
            // TODO: currentDeckCardIds 리스트를 서버나 로컬(JSON/PlayerPrefs)에 저장하는 로직
            Debug.Log($"{currentDeckName} 덱이 성공적으로 저장되었습니다. (총 {currentDeckCardIds.Count}장)");
        }
    }
}
