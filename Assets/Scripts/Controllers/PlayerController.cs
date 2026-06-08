using Unity.Netcode;
using UnityEngine;
using Models.PlayerModels;
using Views.PlayerView;
using Views.EnemyView;
using System;
using System.Collections;
using Managers.LocalDataManagers;
using System.Collections.Generic;
using Controllers.TurnControllers;
using DefaultNamespace;
using Models.TurnModel;
using Cards.PlayableCards;
using Models.CardDatabases;

namespace Controllers.PlayerController
{
    public class PlayerController : NetworkBehaviour
    {

        #region 0. 테스트용
        // ==========================================
        // 🕹️ 통합 멀리건 키 입력 테스트 시스템
        // ==========================================
        private void Update()
        {
            // 1. 내 캐릭터 조종석이 아니라면 입력 차단
            if (!IsOwner) return;

            // 2. 현재 페이즈 정보 가져오기 (PlayerModel에서 쓰신 방식과 동일하게 접근)
            if (TurnModel.Instance == null) return;
            GamePhase currentPhase = TurnModel.Instance.CurrentPhase.Value;
            ulong currentTurnPlayerId = TurnModel.Instance.CurrentTurnPlayerId.Value;

            // ==========================================
            // 🪄 [페이즈 2] 카드 선택 페이즈 조작 (내 턴일 때만)
            // ==========================================
            if (currentPhase == GamePhase.Select && currentTurnPlayerId == NetworkManager.Singleton.LocalClientId)
            {
                HandleSelectInput();
            }
        }

        // ==========================================
        // 🌟 [추가] 카드 선택 입력 처리
        // ==========================================
        private void HandleSelectInput()
        {
            // 숫자키 1~9는 인덱스 0~8, 숫자키 0은 인덱스 9 (10번째 카드)
            if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleSpellIndex(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleSpellIndex(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleSpellIndex(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) ToggleSpellIndex(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) ToggleSpellIndex(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) ToggleSpellIndex(5);
            if (Input.GetKeyDown(KeyCode.Alpha7)) ToggleSpellIndex(6);
            if (Input.GetKeyDown(KeyCode.Alpha8)) ToggleSpellIndex(7);
            if (Input.GetKeyDown(KeyCode.Alpha9)) ToggleSpellIndex(8);
            if (Input.GetKeyDown(KeyCode.Alpha0)) ToggleSpellIndex(9);

            // 스페이스바를 누르면 선택한 카드들로 마법 영창 준비 완료!
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SubmitSpellSelection();
            }
        }
        #endregion
        [Header("MVP References")]
        public PlayerModel model;
        public PlayerView view;
        public EnemyView enemyView;        
        public PlayerUI playerUI;
        public EnemyUI enemyUI;

        public int CurrentHp { get; private set; } = 100;
        public int CurrentMana { get; private set; } = 50;
        private HashSet<int> _selectedSpellIndices = new HashSet<int>();

        
        public override void OnNetworkSpawn() {
            // NGO의 스폰 속보다 UI 초기화보다 빨라져서, 준비될 때까지 기다리게 함
            StartCoroutine(WaitAndInitialize());
        }
        
        private IEnumerator WaitAndInitialize()
        {
            if(IsOwner)
            {
                // 1. 필수 매니저들만 기다림 (UI는 뺐음!)
                while (TurnModel.Instance == null || LocalDataManager.Instance == null)
                {
                    yield return null; 
                }

                // 2. 준비되자마자 서버로 즉시 덱 제출! (이제 MatchManager가 다음으로 넘어감)
                if (model.Deck != null)
                {
                    List<int> myDeck = LocalDataManager.Instance.equippedDeck;
                    model.Deck.SubmitDeckServerRpc(myDeck.ToArray());
                    Debug.Log("🌐 내 덱을 서버로 성공적으로 발송했습니다.");
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            // 구독 해제 (메모리 누수 방지)
            model.CurrentHealth.OnValueChanged -= (oldValue, newValue) => view.UpdateHealth(newValue);
            model.CurrentMana.OnValueChanged -= (oldValue, newValue) => view.UpdateMana(newValue);
            model.Shield.OnValueChanged -= (oldValue, newValue) => view.UpdateShield(newValue);
            model.LastProperty.OnValueChanged -= (oldValue, newValue) => view.UpdateLastProperty(newValue);
            model.CurrentHealth.OnValueChanged -= (oldValue, newValue) => playerUI.UpdateHealth(newValue, model.MaxHealth.Value);
            model.CurrentMana.OnValueChanged -= (oldValue, newValue) => playerUI.UpdateMana(newValue, model.MaxMana.Value, 10);
            model.ActiveStatuses.OnListChanged -= HandleStatusChanged;
            
            model.CurrentHealth.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateHealth(newValue);
            model.CurrentMana.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateMana(newValue);
            model.Shield.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateShield(newValue);
            model.LastProperty.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateLastProperty(newValue);
            model.ActiveStatuses.OnListChanged -= HandleStatusChanged;
            
            // 🌟 팝업이 꺼질 때 Instance가 null일 수 있으므로 널 체크 추가 (? 기호 사용)
            if (TurnModel.Instance != null) TurnModel.Instance.OnPhaseChangedEvent -= HandlePhaseChange;
            if (PlayerUI.Instance != null) PlayerUI.Instance.OnCardClickedAction -= ToggleSpellIndex;
        }
        
        private void HandlePhaseChange(GamePhase phase, bool isMyTurn)
        {
            // 내 캐릭터 컨트롤러가 아니면 무시
            if (!IsOwner) return;

            // 내 턴이 아니게 되거나, 카드 선택(Select) 페이즈를 벗어나면 선택을 강제 초기화합니다.
            if (!isMyTurn || phase != GamePhase.Select)
            {
                ClearSpellSelections();
            }

            // 멀리건은 선공/후공 상관없이 양쪽 플레이어 모두 진행해야 하므로 isMyTurn을 따지지 않음
            if (phase == GamePhase.Mulligan)
            {
                UILoader.Instance.ShowUI("Mulligan_FullScreen", this);
            } 
            // 멀리건이 무사히 끝나고 Draw 페이즈로 넘어가면 화면을 닫음
            else if (phase == GamePhase.Draw)
            {
                UILoader.Instance.HideUI("Mulligan_FullScreen");
            }
        }

        // NetworkList의 이벤트 핸들러
        private void HandleStatusChanged(NetworkListEvent<StatusData> changeEvent)
        {
            // 리스트에 추가, 삭제, 갱신 등 어떤 변화가 생기든
            // View에게 "리스트 전체 줄 테니까 다시 그려!" 라고 던져줌
            playerUI.UpdateStatuses(model.ActiveStatuses);
        }

        public void ToggleSpellIndex(int index)
        {
            if (model.Hand == null || index >= model.Hand.GetLocalHandCount()) return;
            // 1. 내 턴(Select 페이즈)이 아니면 조작 불가하도록 방어
            if (TurnModel.Instance == null || 
                TurnModel.Instance.CurrentPhase.Value != GamePhase.Select || 
                TurnModel.Instance.CurrentTurnPlayerId.Value != NetworkManager.Singleton.LocalClientId) 
            {
                return; 
            }

            // 1. 선택한 카드의 정보와 코스트를 데이터베이스에서 가져옵니다.
            int cardId = model.Hand.GetCardIdAt(index);
            var cardData = CardDatabase.Instance.GetCardById(cardId);
            int cost = cardData != null ? cardData.uiData.cost : 0;

            if (_selectedSpellIndices.Contains(index))
            {
                // 2-A. 선택 취소 시 마나 비용 반환
                _selectedSpellIndices.Remove(index);
                model.ExpectedManaCost -= cost;
                
                if (PlayerUI.Instance != null)
                {
                    PlayerUI.Instance.ToggleCardHighlight(index, false);
                }
            }
            else
            {
                // 2-B. 새로운 카드 선택 시 마나 초과 검증
                if (model.ExpectedManaCost + cost > model.CurrentMana.Value)
                {
                    CommonUIController.Instance.ShowRedAlert("마나가 부족합니다!");
                    return; 
                }

                _selectedSpellIndices.Add(index);
                model.ExpectedManaCost += cost;
                Debug.Log($"[Select] 🪄 {index + 1}번 추가. (예상 마나 소모: {model.ExpectedManaCost} / {model.CurrentMana.Value})");

                if (PlayerUI.Instance != null)
                {
                    PlayerUI.Instance.ToggleCardHighlight(index, true);
                }
            }

            // 3. todo: UI 업데이트 지시 (PlayerView에 예상 코스트를 전달하여 텍스트 색상을 바꾸는 등 시각화)
            // view.UpdateExpectedManaUI(_expectedManaCost, model.CurrentMana.Value);
        }
        
        private void ClearSpellSelections()
        {
            if (_selectedSpellIndices.Count == 0) return;

            // 1. UI의 모든 하이라이트(Selected) 표현 끄기
            if (PlayerUI.Instance != null)
            {
                foreach (int index in _selectedSpellIndices)
                {
                    PlayerUI.Instance.ToggleCardHighlight(index, false);
                }
            }

            // 2. 데이터 및 마나 초기화
            _selectedSpellIndices.Clear();
            model.ExpectedManaCost = 0;
            Debug.Log("카드 선택 및 UI 하이라이트가 모두 초기화되었습니다.");
        }

        private void SubmitSpellSelection()
        {
            List<PlayableCard> selectedCards = new List<PlayableCard>();
            foreach (int index in _selectedSpellIndices)
            {
                int cardId = model.Hand.GetCardIdAt(index);
                var card = CardDatabase.Instance.GetCardById(cardId) as PlayableCard;

                if(card != null) selectedCards.Add(card);
            }

            // 선택 완료! 서버에 '이 카드들로 마법을 준비하겠다'고 선언하고 Incantation 페이즈로 넘어갑니다.
            
            SpellController.Instance.ProcessSpellCast(selectedCards);
            // TODO : 이걸 PhaseManager에게 연락해야함.
            
            ClearSpellSelections();
        }

    }
}
