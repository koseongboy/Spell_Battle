using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Controllers.TurnController;
using Unity.Netcode;
using UnityEngine;

namespace Models.CardModels
{
    public class HandModel : NetworkBehaviour
    {
        // ==========================================
        // 🔒 1. 서버 전용 손패 (보안 및 검증용)
        // ==========================================
        private ObservableCollection<int> serverHand = new ObservableCollection<int>();

        // ==========================================
        // 💻 2. 주인 전용 손패 (UI 렌더링용)
        // ==========================================
        public ObservableCollection<int> localHand = new ObservableCollection<int>();

        // ==========================================
        // 📢 3. 공용 데이터 (상대방 화면의 카드 뒷면 개수)
        // ==========================================
        public NetworkVariable<int> HandCount = new NetworkVariable<int>(0);

        public void Awake()
        {
            // 서버 손패 리스트에 변화가 생기면 자동으로 개수(HandCount) 동기화
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
        // 📥 [서버 영역] 덱에서 카드를 뽑아 넣을 때 (DeckModel이 호출함)
        // ==========================================
        public void AddCardToServerHand(int cardId)
        {
            if (!IsServer) return;
            
            serverHand.Add(cardId); // 추가되는 순간 HandCount 자동 상승

            // 주인의 로컬 손패에도 카드를 추가하라고 귓속말 전송
            ReceiveDrawnCardClientRpc(cardId, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        // ==========================================
        // 📤 [서버 영역] 카드를 사용하거나 멀리건으로 버릴 때 (TurnController가 호출함)
        // ==========================================
        public void RemoveCardFromServerHand(int cardId)
        {
            if (!IsServer) return;
            
            if (serverHand.Remove(cardId)) // 성공적으로 지워졌다면 (HandCount 자동 감소)
            {
                // 주인의 로컬 손패에서도 해당 카드를 지우라고 귓속말 전송
                RemoveCardClientRpc(cardId, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            }
        }
        
        // ==========================================
        // 📤 [서버 영역] 손패에서 무작위 카드 하나를 버리고 그 ID를 반환
        // ==========================================
        public int DiscardRandomCardFromServer()
        {
            if (!IsServer || serverHand.Count == 0) return -1;

            // 1. 무작위 인덱스 추첨
            int randomIndex = Random.Range(0, serverHand.Count);
            int cardToDiscard = serverHand[randomIndex];

            // 2. 기존에 만들어두신 함수 재활용 (주인 로컬 손패 제거 RPC 자동 전송됨)
            RemoveCardFromServerHand(cardToDiscard);

            return cardToDiscard;
        }

        // ==========================================
        // 📨 [클라이언트 영역] 서버의 귓속말을 받는 RPC 함수들
        // ==========================================
        
        // 1. 카드 추가 알림 수신
        [Rpc(SendTo.SpecifiedInParams)]
        private void ReceiveDrawnCardClientRpc(int cardId, RpcParams rpcParams = default)
        {
            localHand.Add(cardId); // 로컬 리스트에 추가 (UI가 이 리스트를 구독하여 화면에 그림)
            Debug.Log($"[Client] {cardId}번 카드가 손패에 들어왔습니다.");
        }

        // 2. 카드 제거 알림 수신 (멀리건, 마법 발동 시)
        [Rpc(SendTo.SpecifiedInParams)]
        private void RemoveCardClientRpc(int cardId, RpcParams rpcParams = default)
        {
            localHand.Remove(cardId); // 로컬 리스트에서 제거 (UI가 구독하여 화면에서 카드를 치움)
            Debug.Log($"[Client] {cardId}번 카드가 손패에서 제거되었습니다.");
        }
    }
}
