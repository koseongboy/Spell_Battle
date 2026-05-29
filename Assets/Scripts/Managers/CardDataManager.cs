using System.Collections.Generic;
using Cards.EffectInfos;
using UnityEngine;
using Cards.PlayableCards;

namespace Managers.DataManagers
{
    public class CardDataManager : MonoBehaviour
    {
        public static CardDataManager Instance { get; private set; }

        // 🌟 빠른 검색을 위한 핵심 데이터 구조 (Key: 카드 ID, Value: 실제 SO 데이터)
        private Dictionary<int, GenericCard> cardDictionary = new Dictionary<int, GenericCard>();

        private void Awake()
        {
            // 싱글톤 중복 생성 방지
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 변경되어도 파괴되지 않음

            LoadAllCards();
        }

        private void LoadAllCards()
        {
            // "Resources/Cards" 폴더 및 모든 하위 폴더의 GenericCard SO를 배열로 불러옵니다.
            GenericCard[] allCards = Resources.LoadAll<GenericCard>("Cards/PlayableCard");

            foreach (var card in allCards)
            {
                if (card != null && card.uiData != null)
                {
                    // 딕셔너리에 ID를 키값으로 저장
                    if (!cardDictionary.ContainsKey(card.uiData.id))
                    {
                        cardDictionary.Add(card.uiData.id, card);
                    }
                    else
                    {
                        Debug.LogWarning($"[CardDataManager] 중복된 카드 ID가 존재합니다! ID: {card.uiData.id}");
                    }
                }
            }
            
            Debug.Log($"[CardDataManager] 총 {cardDictionary.Count}장의 카드를 성공적으로 메모리에 로드했습니다.");
        }
        
        
        // ==========================================
        // 🔍 외부에서 모든 Card의 SO를 가져가는 함수
        // ==========================================
        public List<GenericCard> GetAllCards()
        {
            return new List<GenericCard>(cardDictionary.Values);
        }

        // ==========================================
        // 🔍 외부에서 카드 ID로 SO를 가져갈 때 사용하는 함수
        // ==========================================
        public GenericCard GetCardById(int id)
        {
            if (cardDictionary.TryGetValue(id, out GenericCard card))
            {
                return card;
            }
            
            Debug.LogError($"[CardDataManager] ID가 {id}인 카드를 찾을 수 없습니다! 엑셀 데이터를 확인해주세요.");
            return null;
        }
    }
}