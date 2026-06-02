using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cards.CardUIDatas;
using Managers.LocalDataManagers;
using Models.CardDatabases;
using UnityEngine;
using Models.PlayerModels;

namespace Managers {
    // 덱 정보
    [System.Serializable]
    public class DeckData {
        public string id; 
        public string deckName;
        public List<int> cardIds = new List<int>();
        public string cardCountSummary; 
        public Property representativeProperty;

        public DeckData(string id, string name, List<int> ids, string summary, Property repProp) {
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
            this.deckName = name;
            this.cardIds = new List<int>(ids);
            this.cardCountSummary = summary;
            this.representativeProperty = repProp;
        }
    }
    
    // PlayerPrefs에 여러 덱을 한 번에 JSON으로 저장하기 위한 래퍼 클래스
    [System.Serializable]
    public class DeckStorageWrapper {
        public List<DeckData> decks = new List<DeckData>();
    }
    
    
    public class DeckManager : MonoBehaviour {
        public static DeckManager Instance { get; private set; }

        public List<DeckData> savedDecks = new List<DeckData>(); 

        private void Awake() {
            if (Instance == null) {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 비동기 로드 실행 (Fire-and-Forget)
                _ = LoadDecksAsync(); 
            } else {
                Destroy(gameObject);
            }
        }

        // ==========================================
        // 🔍 CRUD: Read (불러오기 / 조회)
        // ==========================================
        public async Task LoadDecksAsync() {
            bool isServerActive = false;  // TODO : 서버 붙으면 수정

            if (isServerActive) {
                Debug.Log("🌐 [서버] 유저의 덱 리스트를 요청합니다...");
                await Task.Delay(300); 
            } else {
                if (PlayerPrefs.HasKey("SavedDecks_Local")) {
                    string json = PlayerPrefs.GetString("SavedDecks_Local");
                    DeckStorageWrapper wrapper = JsonUtility.FromJson<DeckStorageWrapper>(json);
                    
                    if (wrapper != null && wrapper.decks != null) {
                        this.savedDecks = wrapper.decks;
                        Debug.Log($"[DeckManager] 로컬에서 {savedDecks.Count}개의 덱을 불러왔습니다.");
                        return;
                    }
                }
                Debug.Log("[DeckManager] 저장된 덱이 없어 빈 리스트로 시작합니다.");
            }
        }

        // ==========================================
        // 🔍 CRUD: Read (불러오기 / 조회)
        // ==========================================
        public DeckData GetDeck(string deckId) {
            return savedDecks.FirstOrDefault(d => d.id == deckId);
        }

        public List<DeckData> GetAllDecks() {
            return savedDecks;
        }

        // ==========================================
        // ✏️ CRUD: Create & Update (생성 및 수정)
        // ==========================================
        public async Task<string> CreateOrUpdateDeckAsync(string deckId, string deckName, List<int> cardIds) {
            string summary = GenerateDeckSummary(cardIds);
            Property repProp = CalculateRepresentativeProperty(cardIds); 

            DeckData existingDeck = GetDeck(deckId);

            if (existingDeck != null) {
                existingDeck.deckName = deckName;
                existingDeck.cardIds = new List<int>(cardIds);
                existingDeck.cardCountSummary = summary; 
                existingDeck.representativeProperty = repProp; // 업데이트
                Debug.Log($"[DeckManager] '{deckName}' 덱 업데이트 완료. (대표 속성: {repProp})");
        
                await SaveDecksAsync();
                return existingDeck.id;
            } else {
                DeckData newDeck = new DeckData(null, deckName, cardIds, summary, repProp); // 신규 생성
                savedDecks.Add(newDeck);
                Debug.Log($"[DeckManager] '{deckName}' 덱 생성 완료. (대표 속성: {repProp})");
        
                await SaveDecksAsync();
                return newDeck.id;
            }
        }
        
        // ==========================================
        // 🌟 덱 요약 문자열 생성 헬퍼 함수
        // ==========================================
        private string GenerateDeckSummary(List<int> cardIds) {
            if (cardIds == null || cardIds.Count == 0) {
                return "빈 덱";
            }

            // 속성별 개수를 카운트할 딕셔너리
            Dictionary<Property, int> propertyCounts = new Dictionary<Property, int>();

            foreach (int id in cardIds) {
                var card = CardDatabase.GetCardById(id);
                if (card != null) {
                    Property prop = card.uiData.property;
                    if (propertyCounts.ContainsKey(prop)) {
                        propertyCounts[prop]++;
                    } else {
                        propertyCounts[prop] = 1;
                    }
                }
            }

            // 카운트된 결과를 "속성 개수" 형태의 문자열 리스트로 변환
            List<string> summaryParts = new List<string>();
            foreach (var kvp in propertyCounts) {
                string propKoreanName = GetPropertyKoreanName(kvp.Key);
                summaryParts.Add($"{propKoreanName} {kvp.Value}");
            }

            // "기본 30 불 10 생명 5" 형태로 합쳐서 반환
            return string.Join(" ", summaryParts);
        }
        
        // 🌟 Property Enum을 한글 문자열로 변환해주는 함수
        private string GetPropertyKoreanName(Property prop) {
            switch (prop) {
                case Property.Fire: return "불";
                case Property.Water: return "물";
                case Property.Ground: return "흙";
                case Property.Thunder: return "번개";
                case Property.Wind: return "바람";
                case Property.Ice: return "얼음";
                case Property.Void: return "공허";
                case Property.Vision: return "비전";
                case Property.Life: return "생명";
                default: return "기본";
            }
        }
        
        // ==========================================
// 🌟 대표 속성 계산 로직
// ==========================================
        private Property CalculateRepresentativeProperty(List<int> cardIds) {
            if (cardIds == null || cardIds.Count == 0) {
                return Property.None;
            }

            Dictionary<Property, int> counts = new Dictionary<Property, int>();
            Dictionary<Property, int> firstAppearance = new Dictionary<Property, int>();

            // 1. 순회하며 개수와 최초 등장 위치(Index) 기록
            for (int i = 0; i < cardIds.Count; i++) {
                var card = CardDatabase.GetCardById(cardIds[i]);
                if (card != null) {
                    Property prop = card.uiData.property;

                    if (counts.ContainsKey(prop)) {
                        counts[prop]++;
                    } else {
                        counts[prop] = 1;
                        firstAppearance[prop] = i; // 처음 등장한 인덱스 기록
                    }
                }
            }

            Property repProp = Property.None;
            int maxCount = -1;
            int earliestIndex = int.MaxValue;

            // 2. 최대 개수 및 동률 시 최초 등장 위치 판별
            foreach (var kvp in counts) {
                Property p = kvp.Key;
                int count = kvp.Value;
                int firstIdx = firstAppearance[p];

                // 개수가 더 많으면 무조건 갱신
                if (count > maxCount) {
                    maxCount = count;
                    repProp = p;
                    earliestIndex = firstIdx;
                } 
                // 개수가 같을 경우, 등장 인덱스가 더 앞서는(작은) 속성으로 갱신
                else if (count == maxCount) {
                    if (firstIdx < earliestIndex) {
                        repProp = p;
                        earliestIndex = firstIdx;
                    }
                }
            }

            return repProp;
        }

        // ==========================================
        // 🗑️ CRUD: Delete (삭제)
        // ==========================================
        public async Task DeleteDeckAsync(string deckId) {
            DeckData targetDeck = GetDeck(deckId);

            if (targetDeck != null) {
                savedDecks.Remove(targetDeck);
                Debug.Log($"[DeckManager] 덱(ID: {deckId})을 삭제했습니다.");
                await SaveDecksAsync();
            } else {
                Debug.LogWarning($"[DeckManager] 삭제하려는 덱(ID: {deckId})이 존재하지 않습니다.");
            }
        }   

        // ==========================================
        // 💾 서버/로컬 통합 저장
        // ==========================================
        private async Task SaveDecksAsync() {
            bool isServerActive = false; 

            DeckStorageWrapper wrapper = new DeckStorageWrapper { decks = this.savedDecks };
            string json = JsonUtility.ToJson(wrapper);

            if (isServerActive) {
                Debug.Log("🌐 [서버] 변경된 덱 리스트를 서버에 저장합니다...");
                await Task.Delay(300); 
            } else {
                PlayerPrefs.SetString("SavedDecks_Local", json);
                PlayerPrefs.Save();
            }
        }

        // ==========================================
        // ⚔️ 덱 장착
        // ==========================================
        public void EquipDeckById(string targetDeckId) {
            DeckData targetDeck = GetDeck(targetDeckId);
    
            if (targetDeck != null) {
                LocalDataManager.Instance.equippedDeck = targetDeck.cardIds;
                Debug.Log($"[DeckManager] 덱 장착 완료 (ID: {targetDeckId})");
            } else {
                Debug.LogError($"[DeckManager] 장착하려는 덱(ID: {targetDeckId})을 찾을 수 없습니다!");
            }
        }
    }
}