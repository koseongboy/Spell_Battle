using Unity.Netcode;
using UnityEngine;
using Models.PlayerModels;
using Views.PlayerView;
using Views.EnemyView;
using System;
using Managers.LocalDataManagers;
using System.Collections.Generic;
using Controllers.TurnControllers;
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
            // 🃏 [페이즈 1] 멀리건 페이즈 조작
            // ==========================================
            if (currentPhase == GamePhase.Mulligan)
            {
                HandleMulliganInput();
            }
            // ==========================================
            // 🪄 [페이즈 2] 카드 선택 페이즈 조작 (내 턴일 때만)
            // ==========================================
            else if (currentPhase == GamePhase.Select && currentTurnPlayerId == NetworkManager.Singleton.LocalClientId)
            {
                HandleSelectInput();
            }
        }

        // ==========================================
        // [함수 분리] 멀리건 입력 처리
        // ==========================================
        private void HandleMulliganInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleMulliganIndex(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleMulliganIndex(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleMulliganIndex(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) ToggleMulliganIndex(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) ToggleMulliganIndex(4);

            if (Input.GetKeyDown(KeyCode.M))
            {
                SubmitFinalMulligan();
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

        public int CurrentHp { get; private set; } = 100;
        public int CurrentMana { get; private set; } = 50;
        private HashSet<int> _selectedMulliganIndices = new HashSet<int>();
        private HashSet<int> _selectedSpellIndices = new HashSet<int>();

        
        

        public override void OnNetworkSpawn() {
            // ==========================================
            // 🌟 1. 방어선: 내 캐릭터가 아니면 UI 세팅을 '완벽하게' 무시하고 즉시 종료!
            // (상대방 캐릭터가 스폰될 때 여기서 차단되므로 에러가 절대 안 납니다)
            // ==========================================
            if(IsOwner)
            {
                // 내 캐릭터라면 내 화면 아래쪽(PlayerView)에 연결!
                PlayerView view = PlayerView.Instance;
                if (view != null) 
                {
                    view.Bind(this.model); 
                }
                else 
                {
                    Debug.LogError("씬에 PlayerView(UI)가 없습니다!");
                }
                // ==========================================
                // 🌟 3. [핵심] 내 로컬 덱을 꺼내서 서버로 제출!
                // ==========================================
                if (LocalDataManager.Instance != null && model.Deck != null)
                {
                    // 내 주머니에서 덱 리스트를 꺼내옵니다.
                    List<int> myDeck = LocalDataManager.Instance.equippedDeck;
                    
                    // 리스트를 배열(.ToArray())로 변환해서 서버(DeckModel)로 쏴줍니다!
                    model.Deck.SubmitDeckServerRpc(myDeck.ToArray());
                    
                    Debug.Log("🌐 내 덱을 서버(DeckModel)로 성공적으로 발송했습니다.");
                }
                else
                {
                    Debug.LogError("LocalDataManager 또는 DeckModel이 없어서 덱을 제출할 수 없습니다!");
                }
            }
            else
            {
                EnemyView enemyView = EnemyView.Instance;
                if (enemyView != null) 
                {
                    enemyView.Bind(this.model); 
                }
                else 
                {
                    Debug.LogError("씬에 EnemyView(UI)가 없습니다!");
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
            model.ActiveStatuses.OnListChanged -= HandleStatusChanged;
            
            model.CurrentHealth.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateHealth(newValue);
            model.CurrentMana.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateMana(newValue);
            model.Shield.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateShield(newValue);
            model.LastProperty.OnValueChanged -= (oldValue, newValue) => enemyView.UpdateLastProperty(newValue);
            model.ActiveStatuses.OnListChanged -= HandleStatusChanged;
        }

        // NetworkList의 이벤트 핸들러
        private void HandleStatusChanged(NetworkListEvent<StatusData> changeEvent)
        {
            // 리스트에 추가, 삭제, 갱신 등 어떤 변화가 생기든
            // View에게 "리스트 전체 줄 테니까 다시 그려!" 라고 던져줌
            view.UpdateStatuses(model.ActiveStatuses);
        }

        // ==========================================
        // 🔄 숫자키 입력 시 등록 / 취소를 껐다 켜는 토글 함수 (라고는 하지만 실제 ui 구현 시에도 사용하면 좋을 것 같아서 아래 배치)
        // ==========================================
        private void ToggleMulliganIndex(int index)
        {
            // [안전장치] 현재 내 손패 장수보다 큰 숫자를 누르면 무시
            // 🚨 주석 해제하여 본인의 HandModel 구조에 맞게 수정하세요 (예: model.Hand.CurrentHand.Count 등)
            // if (model.Hand == null || model.Hand.GetTotalCardCount() <= index) return;

            if (_selectedMulliganIndices.Contains(index))
            {
                // 이미 등록되어 있다면 목록에서 제거 (취소)
                _selectedMulliganIndices.Remove(index);
                Debug.Log($"[Mulligan Test] ❌ {index + 1}번 카드 교체 등록을 '취소'했습니다.");
            }
            else
            {
                // 목록에 없다면 추가 (등록)
                _selectedMulliganIndices.Add(index);
                Debug.Log($"[Mulligan Test] 🛡️ {index + 1}번 카드를 교체 대상으로 '등록'했습니다.");
            }
        }

        private void ToggleSpellIndex(int index)
        {
            if (model.Hand == null || index >= model.Hand.GetLocalHandCount()) return;

            // 1. 선택한 카드의 정보와 코스트를 데이터베이스에서 가져옵니다.
            int cardId = model.Hand.GetCardIdAt(index);
            var cardData = Models.CardDatabases.CardDatabase.GetCardById(cardId);
            int cost = cardData != null ? cardData.uiData.cost : 0;

            if (_selectedSpellIndices.Contains(index))
            {
                // 2-A. 선택 취소 시 마나 비용 반환
                _selectedSpellIndices.Remove(index);
                model.ExpectedManaCost -= cost;
                Debug.Log($"[Select] ❌ {index + 1}번 취소. (예상 마나 소모: {model.ExpectedManaCost} / {model.CurrentMana.Value})");
            }
            else
            {
                // 2-B. 새로운 카드 선택 시 마나 초과 검증
                if (model.ExpectedManaCost + cost > model.CurrentMana.Value)
                {
                    Debug.LogWarning("[Select] 🚫 마나가 부족하여 이 카드를 선택할 수 없습니다!");
                    // TODO: PlayerView를 통해 화면 중앙에 "마나 부족!" 경고 텍스트 띄우기
                    return; 
                }

                _selectedSpellIndices.Add(index);
                model.ExpectedManaCost += cost;
                Debug.Log($"[Select] 🪄 {index + 1}번 추가. (예상 마나 소모: {model.ExpectedManaCost} / {model.CurrentMana.Value})");
            }

            // 3. todo: UI 업데이트 지시 (PlayerView에 예상 코스트를 전달하여 텍스트 색상을 바꾸는 등 시각화)
            // view.UpdateExpectedManaUI(_expectedManaCost, model.CurrentMana.Value);
        }

        // ==========================================
        // 🚀 M키 입력 시 서버로 Rpc 통신을 날리는 함수
        // ==========================================
        private void SubmitFinalMulligan()
        {
            List<int> replaceCardIds = new List<int>();

            // 내가 선택한 손패의 인덱스 번호들을 실제 '카드 고유 ID'로 변환합니다.
            foreach (int index in _selectedMulliganIndices)
            {
                // 🚨 [필독] 현재 사용 중이신 HandModel 내부에서 'index'로 카드 고유 ID(int)를 
                // 꺼내오는 실제 변수명이나 함수명으로 이 부분을 맞춰주셔야 합니다!
                // 예: int cardId = model.Hand.List[index]; 등
                int cardId = model.Hand.GetCardIdAt(index); 
                
                replaceCardIds.Add(cardId);
            }

            if (replaceCardIds.Count == 0)
            {
                Debug.Log("[Mulligan Test] 🃏 선택된 카드가 없습니다. 초기 손패 그대로 멀리건을 패스합니다! (M키)");
            }
            else
            {
                Debug.Log($"[Mulligan Test] 🚀 총 {replaceCardIds.Count}장의 카드 교체를 서버에 요청합니다! (M키)");
            }

            // 서버 TurnController의 Rpc 접수처로 발송
            TurnController.Instance.SubmitMulliganServerRpc(replaceCardIds.ToArray());

            // 다음 테스트를 위해 내가 선택했던 기록 깨끗이 비우기
            _selectedMulliganIndices.Clear();
        }

        private void SubmitSpellSelection()
        {
            List<PlayableCard> selectedCards = new List<PlayableCard>();
            foreach (int index in _selectedSpellIndices)
            {
                int cardId = model.Hand.GetCardIdAt(index);
                var card = CardDatabase.GetCardById(cardId) as PlayableCard;

                if(card != null) selectedCards.Add(card);
            }

            // 선택 완료! 서버에 '이 카드들로 마법을 준비하겠다'고 선언하고 Incantation 페이즈로 넘어갑니다.
            TurnController.Instance.ProcessSpellCast(selectedCards);
            
            _selectedSpellIndices.Clear();
            model.ExpectedManaCost = 0;
        }

    }
}
