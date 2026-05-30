using Unity.Netcode;
using UnityEngine;
using Models.PlayerModels;
using Views.PlayerView;
using Views.EnemyView;
using System;
using Managers.LocalDataManagers;
using System.Collections.Generic;
using Controllers.TurnControllers;

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
            // 내 캐릭터 조종석이 아니라면 키보드 입력을 철저히 차단
            if (!IsOwner) return;

            // 1️⃣ 숫자키 1 ~ 5를 눌러서 교체할 카드 번호 지정 (토글 방식)
            if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleMulliganIndex(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleMulliganIndex(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleMulliganIndex(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) ToggleMulliganIndex(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) ToggleMulliganIndex(4);

            // 2️⃣ M키를 누르면 최종 결정된 카드들을 서버로 전송
            if (Input.GetKeyDown(KeyCode.M))
            {
                SubmitFinalMulligan();
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

        public override void OnNetworkDespawn()
        {
            // 구독 해제 (메모리 누수 방지)
            model.CurrentHealth.OnValueChanged -= (oldValue, newValue) => view.UpdateHealth(newValue);
            model.CurrentMana.OnValueChanged -= (oldValue, newValue) => view.UpdateMana(newValue);
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
        // 🔄 숫자키 입력 시 등록 / 취소를 껐다 켜는 토글 함수
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

    }
}
