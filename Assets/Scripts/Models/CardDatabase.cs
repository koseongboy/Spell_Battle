using UnityEngine;
using System.Collections.Generic;
using Cards.PlayableCards;

namespace Models.CardDatabases
{
    public static class CardDatabase
    {
        private static Dictionary<int, PlayableCard> cardMap;
        private static void Initialize()
        {
            if (cardMap != null) return;

            cardMap = new Dictionary<int, PlayableCard>();

            PlayableCard[] loadedCards = Resources.LoadAll<PlayableCard>("Cards");

            foreach(PlayableCard card in loadedCards)
            {
                if (!cardMap.ContainsKey(card.Id))
                {
                    cardMap.Add(card.Id, card);
                } 
                else
                {
                    Debug.LogError($"{card.Name}가 중복되어있습니다. 지워주세요");
                }
            }
            Debug.Log($"총 {cardMap.Count}장의 카드 로드 성공!");
        }

        public static PlayableCard GetCard(int cardId)
        {
            if (cardMap == null) Initialize();
            if(cardMap.TryGetValue(cardId, out PlayableCard card))
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
