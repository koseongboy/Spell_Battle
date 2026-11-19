using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Controllers.SpellControllers;
using Unity.Netcode;
using UnityEngine;

namespace Models.CardModels
{
    /// <summary>
    /// 손패는 비공개 정보이므로 NetworkList로 전체 동기화하지 않고 세 갈래로 나눠 관리한다.
    /// - serverHand : 서버가 보유한 원본 (카드 사용 검증 기준)
    /// - localHand  : 주인 클라이언트에만 Targeted RPC로 반영되는 사본 (UI 렌더링용)
    /// - HandCount  : 상대에게 공개되는 장수 정보
    /// </summary>
    public class HandModel : NetworkBehaviour
    {
        // 서버 전용 원본 손패
        private ObservableCollection<int> serverHand = new ObservableCollection<int>();

        // 주인 클라이언트의 손패 사본 (UI가 CollectionChanged를 구독)
        public ObservableCollection<int> localHand = new ObservableCollection<int>();

        // 공개 정보: 손패 장수 (상대 화면의 카드 뒷면 표시용)
        public NetworkVariable<int> HandCount = new NetworkVariable<int>(0);

        public void Awake()
        {
            // 서버 손패가 바뀔 때마다 HandCount 자동 갱신
            serverHand.CollectionChanged += OnServerHandChanged;
        }

        public override void OnDestroy()
        {
            serverHand.CollectionChanged -= OnServerHandChanged;
        }

        private void OnServerHandChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsServer)
            {
                HandCount.Value = serverHand.Count;
            }
        }

        // ==========================================
        // [서버] 드로우 시 손패에 추가 (DeckModel.DrawCard에서 호출)
        // ==========================================
        public void AddCardToServerHand(int cardId)
        {
            if (!IsServer) return;

            serverHand.Add(cardId);

            // 카드 내용은 주인에게만 전송
            ReceiveDrawnCardClientRpc(cardId, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        // ==========================================
        // [서버] 카드 사용/멀리건 시 손패에서 제거 (SpellController, MatchManager에서 호출)
        // ==========================================
        public bool RemoveCardFromServerHand(int cardId)
        {
            if (!IsServer) return false;

            // 서버 원본에 없는 카드는 제거 실패로 처리 (위조된 카드 ID 방어)
            if (serverHand.Remove(cardId))
            {
                RemoveCardClientRpc(cardId, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
                return true;
            }
            return false;
        }
        
        // ==========================================
        // [서버] 무작위 카드 1장을 버리고 그 ID를 반환 (손패가 비었으면 -1)
        // ==========================================
        public int DiscardRandomCardFromServer()
        {
            if (!IsServer || serverHand.Count == 0) return -1;

            int randomIndex = Random.Range(0, serverHand.Count);
            int cardToDiscard = serverHand[randomIndex];

            RemoveCardFromServerHand(cardToDiscard);

            return cardToDiscard;
        }

        // ==========================================
        // [서버 → 주인 클라이언트] 로컬 손패 동기화 RPC
        // ==========================================

        [Rpc(SendTo.SpecifiedInParams)]
        private void ReceiveDrawnCardClientRpc(int cardId, RpcParams rpcParams = default)
        {
            localHand.Add(cardId);
            Debug.Log($"[Client] {cardId}번 카드가 손패에 들어왔습니다.");
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void RemoveCardClientRpc(int cardId, RpcParams rpcParams = default)
        {
            localHand.Remove(cardId);
            Debug.Log($"[Client] {cardId}번 카드가 손패에서 제거되었습니다.");
        }

        // ==========================================
        // [클라이언트] 로컬 손패 조회
        // ==========================================

        /// <summary>
        /// 로컬 손패의 index 위치 카드 ID를 반환한다. 범위를 벗어나면 -1.
        /// </summary>
        public int GetCardIdAt(int index)
        {
            if (index < 0 || index >= localHand.Count)
            {
                Debug.LogError($"[HandModel] 잘못된 카드 인덱스 요청: {index} (현재 손패 장수: {localHand.Count}장)");
                return -1;
            }

            return localHand[index];
        }

        /// <summary>
        /// 로컬 손패 장수를 반환한다.
        /// </summary>
        public int GetLocalHandCount()
        {
            return localHand.Count;
        }
    }
}
