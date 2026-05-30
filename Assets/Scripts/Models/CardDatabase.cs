using UnityEngine;
using System.Collections.Generic;
using Cards.EffectInfos;
using Cards.PlayableCards;

namespace Models.CardDatabases
{
    public static class CardDatabase
    {
        private static Dictionary<int, GenericCard> cardDictionary;
        private static void Initialize()
        {
            // 이미 로드되었다면 중복 실행 방지
            if (cardDictionary != null) return;

            cardDictionary = new Dictionary<int, GenericCard>();

            // CardDataManager의 경로를 사용하여 데이터 로드
            GenericCard[] allCards = Resources.LoadAll<GenericCard>("Cards/PlayableCard");

            foreach (var card in allCards)
            {
                // 데이터 안정성 체크 (우리가 작성했던 로직 유지)
                if (card != null && card.uiData != null)
                {
                    if (!cardDictionary.ContainsKey(card.uiData.id))
                    {
                        cardDictionary.Add(card.uiData.id, card);
                    }
                    else
                    {
                        Debug.LogWarning($"[CardDatabase] 중복된 카드 ID가 존재합니다! 지워주세요. ID: {card.uiData.id}");
                    }
                }
            }
        
            Debug.Log($"[CardDatabase] 총 {cardDictionary.Count}장의 카드를 성공적으로 메모리에 로드했습니다.");
        }
        
        // ==========================================
        // 🔍 외부에서 모든 Card의 SO를 가져가는 함수
        // ==========================================
        public static List<GenericCard> GetAllCards()
        {
            // 데이터가 아직 로드되지 않았다면 초기화
            if (cardDictionary == null) Initialize();
        
            return new List<GenericCard>(cardDictionary.Values);
        }
        

        // ==========================================
        // 🔍 외부에서 카드 ID로 SO를 가져갈 때 사용하는 함수
        // ==========================================
        public static GenericCard GetCardById(int id)
        {
            // 데이터가 아직 로드되지 않았다면 초기화
            if (cardDictionary == null) Initialize();

            if (cardDictionary.TryGetValue(id, out GenericCard card))
            {
                return card;
            }
        
            Debug.LogError($"[CardDatabase] ID가 {id}인 카드를 찾을 수 없습니다! 엑셀 또는 SO 데이터를 확인해주세요.");
            return null;
        }
    }
}
