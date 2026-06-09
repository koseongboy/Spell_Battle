using System.Collections.Generic;
using System.Threading.Tasks;
using Cards.CardUIDatas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cards.EffectInfos;
using Cards.PlayableCards;
using DefaultNamespace;
using Managers;

namespace Models.CardDatabases
{
    public class CardDatabase : MonoBehaviour
    {
        public static CardDatabase Instance { get; private set; }

        [Header("어드레서블 로드 라벨 설정")]
        [SerializeField] private AssetLabelReference cardLabel;
        [SerializeField] private AssetLabelReference keywordLabel;
        [SerializeField] private AssetLabelReference elementLabel;
        
        private Dictionary<int, PlayableCard> _cardDictionary;
        private Dictionary<CardKeyword, KeywordData> _keywordDictionary;
        private Dictionary<Property, ElementUIData> _elementDictionary;


        public bool IsReady { get; private set; } = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                _ = InitializeAsync();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public async Task InitializeAsync()
        {
            _cardDictionary = new Dictionary<int, PlayableCard>();
            _keywordDictionary = new Dictionary<CardKeyword, KeywordData>();
            _elementDictionary = new Dictionary<Property, ElementUIData>();

            // ==========================================
            // 1. 플레이어블 카드 SO 로드
            // ==========================================
            var cardHandle = Addressables.LoadAssetsAsync<PlayableCard>(cardLabel.labelString, null);
            await cardHandle.Task;

            if (cardHandle.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var card in cardHandle.Result)
                {
                    if (card != null && card.uiData != null)
                    {
                        if (!_cardDictionary.ContainsKey(card.uiData.id))
                        {
                            _cardDictionary.Add(card.uiData.id, card);
                        }
                        else
                        {
                            Debug.LogWarning($"[CardDatabase] 중복된 카드 ID: {card.uiData.id}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("[CardDatabase] 카드 어드레서블 로드 실패!");
            }

            // ==========================================
            // 2. 키워드 SO 로드
            // ==========================================
            var keywordHandle = Addressables.LoadAssetsAsync<KeywordData>(keywordLabel.labelString, null);
            await keywordHandle.Task;

            if (keywordHandle.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var kw in keywordHandle.Result)
                {
                    if (kw != null)
                    {
                        if (!_keywordDictionary.ContainsKey(kw.Keyword))
                        {
                            _keywordDictionary.Add(kw.Keyword, kw);
                        }
                        else
                        {
                            Debug.LogWarning($"[CardDatabase] 중복된 키워드: {kw.Keyword}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("[CardDatabase] 키워드 어드레서블 로드 실패!");
            }            
            
            // ==========================================
            // 3. 속성 SO 로드
            // ==========================================
            var elementHandle = Addressables.LoadAssetsAsync<ElementUIData>(elementLabel.labelString, null);
            await elementHandle.Task;

            if (elementHandle.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var el in elementHandle.Result)
                {
                    if (el != null)
                    {
                        if (!_elementDictionary.ContainsKey(el.Property))
                        {
                            _elementDictionary.Add(el.Property, el);
                        }
                        else
                        {
                            Debug.LogWarning($"[CardDatabase] 중복된 키워드: {el.Property}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("[CardDatabase] 속성 어드레서블 로드 실패!");
            }

            IsReady = true;
            Debug.Log("[CardDatabase] 모든 Data 로드 완료");
        }

        // ==========================================
        // 🔍 외부 데이터 조회 함수 (CRUD: Read)
        // ==========================================
        public List<PlayableCard> GetAllCards()
        {
            if (!IsReady) return new List<PlayableCard>();
            return new List<PlayableCard>(_cardDictionary.Values);
        }

        public PlayableCard GetCardById(int id)
        {
            if (_cardDictionary != null && _cardDictionary.TryGetValue(id, out PlayableCard card))
            {
                return card;
            }
        
            Debug.LogError($"[CardDatabase] ID가 {id}인 카드를 찾을 수 없습니다!");
            return null;
        }

        public bool TryGetKeywordData(CardKeyword keyword, out string title, out string desc)
        {
            if (_keywordDictionary != null && _keywordDictionary.TryGetValue(keyword, out KeywordData data))
            {
                title = data.Title;
                desc = data.Description;
                return true;
            }
            
            title = string.Empty;
            desc = string.Empty;
            return false;
        }
        
        public ElementUIData GetElementData(Property property)
        {
            if (_elementDictionary != null && _elementDictionary.TryGetValue(property, out ElementUIData data))
            {
                return data;
            }
            return null;
        }
        

    }
}