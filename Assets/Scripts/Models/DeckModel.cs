using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Unity.Netcode;
using UnityEngine;

namespace Models.CardModels
{
    public class DeckModel : NetworkBehaviour
    {
        // OnValueChanged가 기본적으로 내장되어있는 C# 표준 라이브러리
        private ObservableCollection<int> currentDeck = new ObservableCollection<int>();
        
        public NetworkVariable<int> DeckCount = new NetworkVariable<int>(0);

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
        // 1. 초기화 (의존성 주입)
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
            if (DeckCount.Value == 0) return; //todo: 혹은 덱에 카드가 없을 때의 로직

            int drawnCardId = currentDeck[0];
            currentDeck.RemoveAt(0);
            
            Hand.AddCardToServerHand(drawnCardId);
        }

        // ==========================================
        // 4. 다시 집어넣기 (특수 효과용)
        // ==========================================
        public void InsertCard(int cardId, bool shuffleAfter = true)
        {
            if (!IsServer) return;
            
            currentDeck.Add(cardId);

            if (shuffleAfter) Shuffle(); // 집어넣고 보통 다시 섞음
        }
    }
}
