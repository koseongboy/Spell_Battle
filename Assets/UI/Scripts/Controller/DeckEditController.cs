using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cards.PlayableCards;
using Cards.CardUIDatas;
using Cards.EffectInfos;
using DA_Assets.Extensions;
using Managers;
using Models.CardDatabases;
using UnityEngine.Serialization;

namespace DefaultNamespace {
    public class DeckEditController : MonoBehaviour {
        [SerializeField] private DeckEdit_FullScreen ui_DeckEdit;

        // --- 데이터 상태 ---
        private List<PlayableCard> allCards = new List<PlayableCard>();
        private List<PlayableCard> currentFilteredCards = new List<PlayableCard>();

        // --- 데이터 상태 ---
        // 이름과 별개로 고유 ID 추적용 변수 추가
        private string currentDeckId = ""; 
        private string currentDeckName = "";
        private List<int> currentDeckCardIds = new List<int>();

        // 필터 상태 (-1이나 None이면 필터 꺼짐)
        private Property currentPropertyFilter = Property.None;
        private int currentCostFilter = -1;

        // --- 페이징 상태 ---
        private int currentPage = 0;
        private const int CARDS_PER_PAGE = 8; // 중앙 윈도우 최대 표시 개수

        private const int MAX_DECK_SIZE = 45;
        private const int MAX_SAME_CARD = 3;
        
        // 현재 팝업에 띄워진 카드가 무엇인지 기억할 변수 추가
        private PlayableCard currentlyViewedCard = null;

        private void Start() {
            CommonUIController.Instance.DoneLoading();
        }

        // View가 OnEnable될 때 스스로 호출하는 함수
        public void RegisterView(DeckEdit_FullScreen newView) {
            ui_DeckEdit = newView;
            allCards = CardDatabase.Instance.GetAllCards();

            // 새로운 View가 등록될 때 기존에 쌓여있던 리스너를 완전히 청소합니다.
            ui_DeckEdit.saveButton.onClick.RemoveAllListeners();
            ui_DeckEdit.clearButton.onClick.RemoveAllListeners();
            ui_DeckEdit.prevPageButton.onClick.RemoveAllListeners();
            ui_DeckEdit.nextPageButton.onClick.RemoveAllListeners();
            ui_DeckEdit.btn_NewDeck.onClick.RemoveAllListeners();
            ui_DeckEdit.btn_CloseNewDeckPopup.onClick.RemoveAllListeners();
            ui_DeckEdit.btn_ConfirmNewDeck.onClick.RemoveAllListeners();
            ui_DeckEdit.btn_DeleteDeck.onClick.RemoveAllListeners();
            ui_DeckEdit.btn_RenameDeck.onClick.RemoveAllListeners();
            ui_DeckEdit.btn_ConfirmRenameDeck.onClick.RemoveAllListeners();
            ui_DeckEdit.btn_CloseRenameDeckPopup.onClick.RemoveAllListeners();

            // 2. 버튼 이벤트 연동
            SetupFilterButtons();
            ui_DeckEdit.saveButton.onClick.AddListener(SaveDeck);
            ui_DeckEdit.clearButton.onClick.AddListener(ClearDeck);
            ui_DeckEdit.prevPageButton.onClick.AddListener(() => ChangePage(-1));
            ui_DeckEdit.nextPageButton.onClick.AddListener(() => ChangePage(1));

            // 새 덱 관련 이벤트 등록
            ui_DeckEdit.btn_NewDeck.onClick.AddListener(OpenNewDeckPopup);
            ui_DeckEdit.btn_CloseNewDeckPopup.onClick.AddListener(CloseNewDeckPopup);
            ui_DeckEdit.btn_ConfirmNewDeck.onClick.AddListener(ConfirmNewDeck);
            
            // 삭제 및 이름 변경 로직
            ui_DeckEdit.btn_DeleteDeck.onClick.AddListener(ConfirmDeleteDeck);
            ui_DeckEdit.btn_RenameDeck.onClick.AddListener(OpenRenameDeckPopup);
            ui_DeckEdit.btn_ConfirmRenameDeck.onClick.AddListener(ConfirmRenameDeck);
            ui_DeckEdit.btn_CloseRenameDeckPopup.onClick.AddListener(CloseRenameDeckPopup);
            
            // 최초 화면 갱신
            RefreshLeftDeckList();
            ApplyFilters(); // 필터 적용 후 페이징 연산 & 화면 갱신
            RefreshRightDeckCards();
        }


        private void SetupFilterButtons() {
            // 속성 필터 연동 (View에 세팅된 리스트를 그대로 순회)
            foreach (var filterUI in ui_DeckEdit.propertyFilters) {
                Property p = filterUI.property;
                var elementData = CardDatabase.Instance.GetElementData(p);
                filterUI.Icon.sprite = elementData.Icon;
                filterUI.Name.text = elementData.Name;
                
                // 버튼 클릭 이벤트 바인딩
                filterUI.button.onClick.RemoveAllListeners();
                filterUI.button.onClick.AddListener(() => TogglePropertyFilter(p));

                // 초기 상태는 모두 하이라이트 꺼짐
                if (filterUI.highlightObj != null)
                    filterUI.highlightObj.SetActive(false);
            }

            // 코스트 필터 자동 연동
            foreach (var filterUI in ui_DeckEdit.costFilters) {
                int cost = filterUI.cost;

                filterUI.button.onClick.RemoveAllListeners();
                filterUI.button.onClick.AddListener(() => ToggleCostFilter(cost));
                if (filterUI.highlightObj != null) filterUI.highlightObj.SetActive(false);
            }
        }

        // ==========================================
        // 필터링 토글 로직
        // ==========================================
        private void TogglePropertyFilter(Property prop) {
            // 이미 켜진 속성을 다시 누르면 필터 해제(None)
            currentPropertyFilter = (currentPropertyFilter == prop) ? Property.None : prop;

            // [UI 시각적 갱신] 선택된 버튼만 하이라이트 켜기
            foreach (var filterUI in ui_DeckEdit.propertyFilters) {
                if (filterUI.highlightObj != null) {
                    // 현재 누른 속성과 일치하면 True(켜짐), 아니면 False(꺼짐)
                    bool isSelected = (filterUI.property == currentPropertyFilter);
                    filterUI.highlightObj.SetActive(isSelected);
                }
            }

            ApplyFilters(); // 데이터 필터링 및 화면 갱신
        }

        private void ToggleCostFilter(int cost) {
            // 이미 선택된 코스트를 다시 누르면 필터 해제(-1)
            currentCostFilter = (currentCostFilter == cost) ? -1 : cost;

            // [UI 시각적 갱신] 선택된 코스트 버튼만 하이라이트 활성화
            foreach (var filterUI in ui_DeckEdit.costFilters) {
                if (filterUI.highlightObj != null) {
                    bool isSelected = (filterUI.cost == currentCostFilter);
                    filterUI.highlightObj.SetActive(isSelected);
                }
            }

            ApplyFilters(); // 데이터 재필터링 및 화면 갱신
        }

        // 필터링 적용 및 페이지 리셋
        private void ApplyFilters() {
            currentFilteredCards = allCards.Where(c =>
                    (currentPropertyFilter == Property.None || c.uiData.property == currentPropertyFilter) &&
                    (currentCostFilter == -1 || c.uiData.cost == currentCostFilter ||
                     (currentCostFilter == 10 && c.uiData.cost >= 10))
                )
                .OrderBy(c => c.uiData.cost)      // 코스트 기준 오름차순
                .ThenBy(c => c.uiData.id)         // 코스트가 같으면 ID 기준 오름차순
                .ToList();

            currentPage = 0; // 필터가 바뀌면 무조건 첫 페이지로 돌아감
            RefreshCenterCards();
        }

        // 페이지 변경
        private void ChangePage(int direction) {
            int maxPage = Mathf.Max(0, Mathf.CeilToInt((float)currentFilteredCards.Count / CARDS_PER_PAGE) - 1);
            currentPage = Mathf.Clamp(currentPage + direction, 0, maxPage);
            RefreshCenterCards();
        }

        // ==========================================
        // 화면 갱신 로직 (View 업데이트)
        // ==========================================
        private void RefreshCenterCards() {
            ui_DeckEdit.ReturnAllCardsToPool();

            int startIndex = currentPage * CARDS_PER_PAGE;
            var pageCards = currentFilteredCards.Skip(startIndex).Take(CARDS_PER_PAGE).ToList();

            foreach (var cardData in pageCards) {
                UI_Card_DeckEdit cardObj = ui_DeckEdit.GetCardFromPool();
                
                // 클릭(팝업), 드롭(추가), 그리고 드롭을 판별할 우측 영역(dropZoneRect)을 넘겨줍니다.
                cardObj.Init(cardData, OnCardClickedToShowPopup, OnCardDroppedToAdd, ui_DeckEdit.dropZoneRect);
            }

            UpdatePaginationUI();
        }

        // 더 이상 갈 곳이 없으면 화살표를 숨김
        private void UpdatePaginationUI() {
            int maxPage = Mathf.Max(0, Mathf.CeilToInt((float)currentFilteredCards.Count / CARDS_PER_PAGE) - 1);

            ui_DeckEdit.prevPageButton.gameObject.SetActive(currentPage > 0);
            ui_DeckEdit.nextPageButton.gameObject.SetActive(currentPage < maxPage);
        }


        private void RefreshRightDeckCards() {
            ui_DeckEdit.ReturnAllCardsInDeckToPool();

            var groupedCards = currentDeckCardIds
                .GroupBy(id => id)
                .Select(group => new {
                    Data = CardDatabase.Instance.GetCardById(group.Key),
                    Count = group.Count()
                })
                .OrderBy(c => c.Data.uiData.cost)
                .ToList();

            foreach (var item in groupedCards) {
                CardInDeckPiece piece = ui_DeckEdit.GetCardInDeckFromPool();
                piece.Init(item.Data, item.Count, OnCardClickedToRemove);
            }

            ui_DeckEdit.deckCountText.text = $"{currentDeckCardIds.Count} / {MAX_DECK_SIZE}";
        }


        private void RefreshLeftDeckList() {
            ui_DeckEdit.ReturnAllDeckListsToPool();
            var allSavedDecks = DeckManager.Instance.GetAllDecks();
            
            // 저장된 덱이 없을 때 임시 덱 띄우기
            if (allSavedDecks == null || allSavedDecks.Count == 0) {
                return;
            }

            foreach (var deck in allSavedDecks) {
                DeckListPiece piece = ui_DeckEdit.GetDeckListFromPool();
        
                bool isSelected = (deck.id == currentDeckId);
                
                piece.Init(deck.deckName, deck.cardCountSummary, deck.representativeProperty, isSelected, (clickedName) => OnDeckSelected(deck.id));
            }
        }
        
        // 덱 리스트에서 특정 덱을 클릭했을 때 실행될 콜백 함수
        private void OnDeckSelected(string deckId) {
            // 팩트: ID가 빈 문자열("")이 아닌 상태에서 같은 ID를 누르면 무시
            if (currentDeckId == deckId && !string.IsNullOrEmpty(deckId)) {
                return; 
            }

            currentDeckId = deckId;
    
            var selectedDeck = DeckManager.Instance.GetDeck(deckId);
            if (selectedDeck != null) {
                currentDeckCardIds = new List<int>(selectedDeck.cardIds);
            } else {
                currentDeckCardIds.Clear();
            }

            RefreshLeftDeckList();
            RefreshRightDeckCards();
        }
        
        // ==========================================
        // 새 덱 생성 팝업 로직
        // ==========================================
        private void OpenNewDeckPopup() {
            ui_DeckEdit.popup_NewDeck.SetActive(true);
            ui_DeckEdit.input_NewDeckName.text = ""; // 열 때마다 이름 초기화
        }

        private void CloseNewDeckPopup() {
            ui_DeckEdit.popup_NewDeck.SetActive(false);
        }
        
        private async void ConfirmNewDeck() {
            string newName = ui_DeckEdit.input_NewDeckName.text.Trim();
            
            if (string.IsNullOrEmpty(newName)) {
                CommonUIController.Instance.ShowRedAlert("덱 이름을 입력해주세요.");
                return;
            }

            // 1. 상태 초기화 (ID를 비우면 CreateOrUpdateDeckAsync에서 새 덱으로 인식하여 발급함)
            currentDeckId = "";
            currentDeckName = newName;
            currentDeckCardIds.Clear();

            // 2. 즉시 빈 덱을 DeckManager에 저장하여 고유 ID 획득
            currentDeckId = await DeckManager.Instance.CreateOrUpdateDeckAsync(currentDeckId, currentDeckName, currentDeckCardIds);
            
            // 3. 팝업 닫기 및 알림
            CloseNewDeckPopup();
            CommonUIController.Instance.ShowBlackAlert($"새 덱 '{currentDeckName}'이(가) 생성되었습니다.");
            
            // 4. 리스트와 편집창 새로고침
            RefreshLeftDeckList();
            RefreshRightDeckCards();
        }
        
        // ==========================================
        // 카드 클릭 시 팝업 호출 로직
        // ==========================================
        private void OnCardClickedToShowPopup(PlayableCard card) {
            // 팩트: 현재 선택된 덱이 없으면 추가 불가(열람 모드)
            bool canAdd = !string.IsNullOrEmpty(currentDeckId);

            CardDetailPayload payload = new CardDetailPayload {
                CardData = card,
                CanAdd = canAdd,
                OnConfirmAdd = ConfirmAddCardFromPopup // 아래 정의된 함수를 델리게이트로 넘김
            };

            // UILoader를 통해 팝업 프리팹을 띄우고 데이터 전달
            UILoader.Instance.ShowUI("CardDetail_Popup", payload);
        }
        
        // ==========================================
        // 드롭: 오른쪽 영역에 놓았을 때 덱에 추가
        // ==========================================
        private void OnCardDroppedToAdd(PlayableCard card) {
            if (string.IsNullOrEmpty(currentDeckId)) {
                CommonUIController.Instance.ShowRedAlert("편집할 덱을 먼저 선택하거나 새로 만들어주세요.");
                return;
            }

            if (currentDeckCardIds.Count >= MAX_DECK_SIZE) {
                CommonUIController.Instance.ShowRedAlert("덱에 카드를 더 추가할 수 없습니다.");
                return;
            }

            int currentCount = currentDeckCardIds.Count(id => id == card.uiData.id);
            if (currentCount >= MAX_SAME_CARD) {
                CommonUIController.Instance.ShowRedAlert($"동일한 카드는 {MAX_SAME_CARD}장까지만 넣을 수 있습니다!");
                return;
            }

            currentDeckCardIds.Add(card.uiData.id);
            RefreshRightDeckCards(); 
        }
        
        // ==========================================
        // 팝업에서 '추가' 버튼을 눌렀을 때 콜백될 함수
        // ==========================================
        private void ConfirmAddCardFromPopup(PlayableCard cardToAdd) {
            if (string.IsNullOrEmpty(currentDeckId)) {
                CommonUIController.Instance.ShowRedAlert("편집할 덱을 먼저 선택해주세요.");
                return;
            }

            if (currentDeckCardIds.Count >= MAX_DECK_SIZE) {
                CommonUIController.Instance.ShowRedAlert("덱에 카드를 더 추가할 수 없습니다.");
                return;
            }

            int currentCount = currentDeckCardIds.Count(id => id == cardToAdd.uiData.id);
            if (currentCount >= MAX_SAME_CARD) {
                CommonUIController.Instance.ShowRedAlert($"동일한 카드는 {MAX_SAME_CARD}장까지만 넣을 수 있습니다!");
                return;
            }

            currentDeckCardIds.Add(cardToAdd.uiData.id);
            RefreshRightDeckCards(); 
        }
        

        // ==========================================
        // 카드 추가 / 제거 로직
        // ==========================================
        private void OnCardClickedToAdd(PlayableCard card) {
            if (string.IsNullOrEmpty(currentDeckId)) {
                return;
            }
            
            if (currentDeckCardIds.Count >= MAX_DECK_SIZE) {
                CommonUIController.Instance.ShowRedAlert("덱에 카드를 더 추가할 수 없습니다.");
                return;
            }

            int currentCount = currentDeckCardIds.Count(id => id == card.uiData.id);
            if (currentCount >= MAX_SAME_CARD) {
                CommonUIController.Instance.ShowRedAlert($"동일한 카드는 {MAX_SAME_CARD}장까지만 넣을 수 있습니다!");
                return;
            }

            currentDeckCardIds.Add(card.uiData.id);
            RefreshRightDeckCards(); // 덱이 변했으니 우측 리스트 갱신
        }

        private void OnCardClickedToRemove(PlayableCard card) {
            currentDeckCardIds.Remove(card.uiData.id); // 한 장만 제거
            RefreshRightDeckCards();
        }

        // ==========================================
        // 저장 및 비우기
        // ==========================================
        private void ClearDeck() {
            currentDeckCardIds.Clear();
            RefreshRightDeckCards();
        }

        private async void SaveDeck() {
            if (currentDeckCardIds.Count == 0) {
                CommonUIController.Instance.ShowRedAlert("빈 덱은 저장할 수 없습니다.");
                return;
            }
            
            // TODO : Deck 정보 저장
            currentDeckId = await DeckManager.Instance.CreateOrUpdateDeckAsync(currentDeckId, currentDeckName, currentDeckCardIds);
    
            CommonUIController.Instance.ShowBlackAlert($"{currentDeckName} 덱 저장 완료!");
    
            // 새 ID가 발급되었거나 이름이 바뀌었을 수 있으므로 좌측 리스트 갱신
            RefreshLeftDeckList(); 
        }
        
        // ==========================================
        // 삭제 로직
        // ==========================================
        private async void ConfirmDeleteDeck() {
            if (string.IsNullOrEmpty(currentDeckId)) {
                CommonUIController.Instance.ShowRedAlert("삭제할 덱이 선택되지 않았습니다.");
                return;
            }
            
            ConfirmPopupData data = new ConfirmPopupData
            {
                message = "덱을 삭제하시겠습니까?",
                onConfirm = DeleteDeck,
                onCancel = () => { }
            };

            UILoader.Instance.ShowUI<ConfirmPopupData>("Confirm_Popup", data);
        }

        private async void DeleteDeck() {
            
            // DeckManager를 통해 삭제 처리
            await DeckManager.Instance.DeleteDeckAsync(currentDeckId);
            CommonUIController.Instance.ShowBlackAlert($"'{currentDeckName}' 덱이 삭제되었습니다.");

            // 삭제 후 현재 선택 상태 초기화
            currentDeckId = "";
            currentDeckName = "";
            currentDeckCardIds.Clear();

            RefreshLeftDeckList();
            RefreshRightDeckCards();
        }

        // ==========================================
        // 이름 변경 팝업 로직
        // ==========================================
        private void OpenRenameDeckPopup() {
            if (string.IsNullOrEmpty(currentDeckId)) {
                CommonUIController.Instance.ShowRedAlert("이름을 변경할 덱이 선택되지 않았습니다.");
                return;
            }
            
            ui_DeckEdit.popup_RenameDeck.SetActive(true);
            // 팝업을 열 때 기존 이름을 입력 필드에 미리 세팅해줍니다.
            ui_DeckEdit.input_RenameDeckName.text = currentDeckName; 
        }

        private void CloseRenameDeckPopup() {
            ui_DeckEdit.popup_RenameDeck.SetActive(false);
        }

        private async void ConfirmRenameDeck() {
            string newName = ui_DeckEdit.input_RenameDeckName.text.Trim();
            
            if (string.IsNullOrEmpty(newName)) {
                CommonUIController.Instance.ShowRedAlert("덱 이름을 입력해주세요.");
                return;
            }

            currentDeckName = newName;
            
            // ID를 유지한 채로 CreateOrUpdateDeckAsync를 호출하면 이름만 덮어쓰기 업데이트 됩니다.
            await DeckManager.Instance.CreateOrUpdateDeckAsync(currentDeckId, currentDeckName, currentDeckCardIds);
            
            CloseRenameDeckPopup();
            CommonUIController.Instance.ShowBlackAlert("덱 이름이 변경되었습니다.");
            
            RefreshLeftDeckList();
        }
    }
}