using UnityEngine;
using System.Collections.Generic;
using Models.Cards.CardBase;

namespace Models.CardDatabase
{
    public static class CardDatabase
    {
        private static Dictionary<int, CardBase> cardMap;
        private static void Initialize()
        {
            if (cardMap != null) return;

            cardMap = new Dictionary<int, CardBase>();

            CardBase[] loadedCards = Resources.LoadAll<CardBase>("Cards");

            foreach(CardBase card in loadedCards)
            {
                if (!cardMap.ContainsKey(card.Id))
                {
                    cardMap.Add(card.Id, card);
                } 
                else
                {
                    Debug.LogError($"{card.CardName}가 중복되어있습니다. 지워주세요");
                }
            }
            Debug.Log($"총 {cardMap.Count}장의 카드 로드 성공!");
        }

        public static CardBase getCard(int cardId)
        {
            if (cardMap == null) Initialize();
            if(cardMap.TryGetValue(cardId, out CardBase card))
            {
                return card;
            }
            else
            {
                Debug.LogError($"{cardId}를 찾을 수 없습니다");
                return null;
            }
        }
    }
}
