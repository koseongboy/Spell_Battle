using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Unity.Netcode;
using UnityEngine;

namespace Models.CardModels
{
    /// <summary>
    /// 덱은 서버만 내용을 알고, 클라이언트에는 남은 장수(DeckCount)만 공개한다.
    /// 셔플·드로우 모두 서버에서 수행해 클라이언트가 덱 순서를 알 수 없게 한다.
    /// </summary>
    public class DeckModel : NetworkBehaviour
    {
        // 서버 전용 덱 (변경 시 CollectionChanged로 DeckCount 자동 갱신)
        private ObservableCollection<int> currentDeck = new ObservableCollection<int>();

        public NetworkVariable<int> DeckCount = new NetworkVariable<int>(0);

        // 덱 제출 완료 여부. MatchManager가 양측 true를 확인한 뒤 게임을 시작한다.
        public NetworkVariable<bool> IsDeckReady = new NetworkVariable<bool>(false);

        [Header("Dependencies")]
        public HandModel Hand;

        public void Awake()
        {
            currentDeck.CollectionChanged += OnDeckChanged;
        }
        public override void OnDestroy()
        {
            currentDeck.CollectionChanged -= OnDeckChanged;
        }

        private void OnDeckChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            DeckCount.Value = currentDeck.Count;
        }


        // ==========================================
        // 1. 초기화
        // ==========================================
        public void InitializeDeck(IEnumerable<int> initialCards)
        {
            if (!IsServer) return;
            currentDeck.Clear();
            foreach(var card in initialCards) currentDeck.Add(card);
        }

        // ==========================================
        // 2. 셔플 (Fisher-Yates 알고리즘)
        // ==========================================
        public void Shuffle()
        {
            if (!IsServer) return;
            for (int i = 0; i < currentDeck.Count; i++)
            {
                int temp = currentDeck[i];
                int randomIndex = Random.Range(i, currentDeck.Count);
                currentDeck[i] = currentDeck[randomIndex];
                currentDeck[randomIndex] = temp;
            }
            Debug.Log($"[Server] 덱을 섞었습니다. (현재 {currentDeck.Count}장)");
        }

        // ==========================================
        // 3. 드로우 (맨 위에서 뽑기)
        // ==========================================
        public void DrawCard()
        {
            if (!IsServer) return;
            if (DeckCount.Value == 0) return; // TODO: 덱 소진 시 페널티 규칙

            int drawnCardId = currentDeck[0];
            currentDeck.RemoveAt(0);
            
            Hand.AddCardToServerHand(drawnCardId);
        }

        // ==========================================
        // 4. 덱에 카드 넣기 (멀리건 반환, 카드 효과용)
        // ==========================================
        public void InsertCard(int cardId, bool shuffleAfter = true)
        {
            if (!IsServer) return;

            currentDeck.Add(cardId);

            // 여러 장을 넣을 때는 false로 호출하고 마지막에 한 번만 Shuffle
            if (shuffleAfter) Shuffle();
        }

        // [주인 클라이언트 → 서버] 선택한 덱 제출. 셔플은 서버에서 수행
        [Rpc(SendTo.Server)]
        public void SubmitDeckServerRpc(int[] deckIds)
        {
            Debug.Log($"덱 초기화 실행됨!");
            InitializeDeck(deckIds);
            Shuffle();

            Debug.Log($"[Server] 플레이어 {OwnerClientId}의 덱 세팅 및 셔플 완료! (총 {DeckCount.Value}장)");

            IsDeckReady.Value = true;
        }
    }
}
