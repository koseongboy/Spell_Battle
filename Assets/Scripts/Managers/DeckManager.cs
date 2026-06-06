using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cards.CardUIDatas;
using DefaultNamespace;
using Managers.LocalDataManagers;
using Models.CardDatabases;
using UnityEngine;
using Models.PlayerModels;
using UnityEngine.Networking;

namespace Managers {
    [Serializable]
    public class ServerDeckDto
    {
        public string userId;
        public string deckName;
        public List<string> cards;
    }
    
    // 덱 정보
    [Serializable]
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
            string serverURL = AuthManager.Instance.serverURL;
            string token = LocalDataManager.Instance.userToken;

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[DeckManager] 유저 토큰이 없습니다. 로컬 덱만 로드합니다.");
                LoadLocalDecks();
                return;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(serverURL + "/decks"))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"<color=#00FF00>[DeckManager] 서버 덱 로드 성공!</color>\n{jsonResponse}");

                    // 1. JSON 배열 파싱
                    ServerDeckDto[] serverDecks = JsonHelper.FromJson<ServerDeckDto>(jsonResponse);
            
                    savedDecks.Clear();

                    // 2. 서버 데이터를 로컬 DeckData 구조에 맞게 변환
                    foreach (var sDeck in serverDecks)
                    {
                        // string ID를 int ID로 변환 (파싱 에러 방지 처리)
                        List<int> parsedCardIds = new List<int>();
                        foreach (string cId in sDeck.cards)
                        {
                            if (int.TryParse(cId, out int intId)) parsedCardIds.Add(intId);
                        }

                        // 요약 정보와 대표 속성 재계산
                        string summary = GenerateDeckSummary(parsedCardIds);
                        Property repProp = CalculateRepresentativeProperty(parsedCardIds);

                        // 고유 ID는 로컬용으로 임의 발급 (서버에 개별 식별자가 없기 때문)
                        DeckData newDeck = new DeckData(Guid.NewGuid().ToString(), sDeck.deckName, parsedCardIds, summary, repProp);
                        savedDecks.Add(newDeck);
                    }

                    // 로컬 동기화 캐싱
                    await SaveDecksLocalAsync();
                }
                else
                {
                    Debug.LogError($"[DeckManager] 서버 덱 로드 실패. 로컬 데이터만 불러옵니다. Error: {request.error}");
                    LoadLocalDecks();
                }
            }
        }
        
        // 기존에 있던 로컬 전용 로드 로직 분리
        private void LoadLocalDecks()
        {
            if (PlayerPrefs.HasKey("SavedDecks_Local")) 
            {
                string json = PlayerPrefs.GetString("SavedDecks_Local");
                DeckStorageWrapper wrapper = JsonUtility.FromJson<DeckStorageWrapper>(json);
        
                if (wrapper != null && wrapper.decks != null) 
                {
                    this.savedDecks = wrapper.decks;
                    Debug.Log($"[DeckManager] 로컬에서 {savedDecks.Count}개의 덱을 불러왔습니다.");
                    return;
                }
            }
            Debug.Log("[DeckManager] 저장된 덱이 없어 빈 리스트로 시작합니다.");
        }
        
        // ==========================================
        // 📡 서버로 덱 공유(저장) API 호출
        // ==========================================
        public async Task<bool> SyncDeckToServerAsync(DeckData localDeck)
        {
            string serverURL = AuthManager.Instance.serverURL;
            string token = LocalDataManager.Instance.userToken;
            string userId = LocalDataManager.Instance.userId;

            if (string.IsNullOrEmpty(token)) return false;

            // 팩트: int ID 리스트를 서버 규격인 string 리스트로 변환
            List<string> stringCardIds = localDeck.cardIds.Select(id => id.ToString()).ToList();

            // 서버 API 규격에 맞춘 데이터 생성
            ServerDeckDto requestDto = new ServerDeckDto
            {
                userId = userId,
                deckName = localDeck.deckName, // 서버는 이 이름을 키(Key)로 쓸 확률이 높음
                cards = stringCardIds
            };

            string jsonData = JsonUtility.ToJson(requestDto);

            using (UnityWebRequest request = new UnityWebRequest(serverURL + "/decks", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
        
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"<color=white>[서버 동기화]</color> '{localDeck.deckName}' 덱 저장 완료.");
                    return true;
                }
                else
                {
                    Debug.LogError($"[서버 동기화 실패] {request.error}");
                    return false;
                }
            }
        }
        
        public async Task<bool> ShareDeckToServerAsync(string deckName, List<int> cardIds)
        {
            string serverURL = AuthManager.Instance.serverURL;
            string token = LocalDataManager.Instance.userToken;
            string userId = LocalDataManager.Instance.userId;

            if (string.IsNullOrEmpty(token)) return false;

            // 1. int 리스트를 string 리스트로 변환
            List<string> stringCardIds = cardIds.Select(id => id.ToString()).ToList();

            ServerDeckDto requestDto = new ServerDeckDto
            {
                userId = userId,
                deckName = deckName,
                cards = stringCardIds
            };

            string jsonData = JsonUtility.ToJson(requestDto);

            using (UnityWebRequest request = new UnityWebRequest(serverURL + "/decks", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
        
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("<color=#00FF00>[DeckManager] 서버에 덱을 성공적으로 저장(공유)했습니다.</color>");
                    return true;
                }
                else
                {
                    Debug.LogError($"[DeckManager] 덱 서버 저장 실패: {request.error}");
                    return false;
                }
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
        
                await SaveDecksLocalAsync();
                return existingDeck.id;
            } else {
                DeckData newDeck = new DeckData(null, deckName, cardIds, summary, repProp); // 신규 생성
                savedDecks.Add(newDeck);
                Debug.Log($"[DeckManager] '{deckName}' 덱 생성 완료. (대표 속성: {repProp})");
        
                await SaveDecksLocalAsync();
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
        // 🗑️ CRUD: Delete (삭제)
        // ==========================================
        public async Task DeleteDeckAsync(string deckId) {
            DeckData targetDeck = GetDeck(deckId);

            if (targetDeck != null) {
                savedDecks.Remove(targetDeck);
                Debug.Log($"[DeckManager] 덱(ID: {deckId})을 삭제했습니다.");
                await SaveDecksLocalAsync();
            } else {
                Debug.LogWarning($"[DeckManager] 삭제하려는 덱(ID: {deckId})이 존재하지 않습니다.");
            }
        }   

        // ==========================================
        // 💾 서버/로컬 통합 저장
        // ==========================================
        private async Task SaveDecksLocalAsync() {
            DeckStorageWrapper wrapper = new DeckStorageWrapper { decks = this.savedDecks };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString("SavedDecks_Local", json);
            PlayerPrefs.Save();
            await Task.Yield();
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
    }
}