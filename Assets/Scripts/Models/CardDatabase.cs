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
    
    public static class KeywordDatabase {
        private static readonly Dictionary<CardKeyword, (string title, string desc)> keywordData = new() {
            { CardKeyword.Ignite, ("발화", "턴이 끝날 때 발화 중첩 당 1의 피해를 입습니다.") },
            { CardKeyword.Riverbend, ("강굽이", "이전 주문의 속성이 물일 경우, 추가 효과가 발동합니다.") },
            { CardKeyword.Freeze, ("빙결", "빙결 중첩이 3이 되면, 중첩이 초기화되고 추가 데미지를 입힙니다.") },
            { CardKeyword.Prophecy, ("예언", "예언 30 중첩을 쌓으면 플레이어가 막강해집니다.") },
            { CardKeyword.Condense, ("응축", "응축 중첩을 모아 '방출' 주문의 위력을 강화합니다.") },
            { CardKeyword.Expose, ("방출", "보유한 응축 중첩을 모두 소모합니다.") },
            { CardKeyword.Reflect, ("반사", "받은 피해의 30%를 적에게 되돌려줍니다.") }
        };

        public static bool TryGetKeywordData(CardKeyword keyword, out string title, out string desc) {
            if (keywordData.TryGetValue(keyword, out var data)) {
                title = data.title;
                desc = data.desc;
                return true;
            }
            title = string.Empty;
            desc = string.Empty;
            return false;
        }
    }
}
