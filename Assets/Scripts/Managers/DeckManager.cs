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
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        
        [SerializeField] private AssetLabelReference presetDeckLabel;
        public List<DeckData> savedDecks = new List<DeckData>(); 
        
        private List<PresetDeckData> _presetDecks;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 비동기 로드 실행 (Fire-and-Forget)

                _ = LoadPresetDecks();
            } else {
                Destroy(gameObject);
            }
        }
        

        // ==========================================
        // 🔍 CRUD: Read (불러오기 / 조회)
        // ==========================================
        
        // ==========================================
        // 프리셋 덱 SO 로드
        // ==========================================
        private async Task LoadPresetDecks() {

            Debug.Log("진입");
            _presetDecks = new List<PresetDeckData>();
            
            var presetHandle = Addressables.LoadAssetsAsync<PresetDeckData>(presetDeckLabel.labelString, null);
            await presetHandle.Task;
            
            if (presetHandle.Status == AsyncOperationStatus.Succeeded) {
                _presetDecks = new List<PresetDeckData>(presetHandle.Result);
                Debug.Log($"[DeckManager] 프리셋 덱 {_presetDecks.Count}개 로드 완료.");
            } else {
                Debug.LogError("[DeckManager] 프리셋 덱 로드 실패!");
            }
        } 
        
                
        // ==========================================
        // 서버에서 Deck 로드
        // ==========================================
        public async Task LoadDecksFromServerAsync() {
            string userId = LocalDataManager.Instance.userId;
            string serverURL = AuthManager.Instance.serverURL;

            // ==========================================
            // 서버에서 덱 로딩
            // ==========================================
            WWWForm form = new WWWForm();
            form.AddField("userId", userId);
            using (UnityWebRequest request = UnityWebRequest.Post(serverURL+"/decks", form))
            {
                request.method = "GET";

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
                        List<int> parsedCardIds = new List<int>();
                        foreach (string cId in sDeck.cards)
                        {
                            if (int.TryParse(cId, out int intId)) parsedCardIds.Add(intId);
                        }

                        string summary = GenerateDeckSummary(parsedCardIds);
                        Property repProp = CalculateRepresentativeProperty(parsedCardIds);

                        DeckData newDeck = new DeckData(Guid.NewGuid().ToString(), sDeck.deckName, parsedCardIds, summary, repProp);
                        savedDecks.Add(newDeck);
                    }

                    // 로컬 동기화 캐싱
                    _ = SaveDecksLocalAsync();
                }
                else
                {
                    Debug.LogError($"[DeckManager] 서버 덱 로드 실패. Error: {request.error}");
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
        // Read (불러오기 / 조회)
        // ==========================================
        public DeckData GetDeck(string deckId) {
            // 1. 유저의 저장된 덱에서 먼저 검색
            DeckData foundDeck = savedDecks.FirstOrDefault(d => d.id == deckId);
            
            // 2. 없다면 프리셋 덱 리스트에서 검색하여 반환
            if (foundDeck == null) {
                foundDeck = GetAllPresetDecks().FirstOrDefault(d => d.id == deckId);
            }
            
            return foundDeck;
        }

        public List<DeckData> GetAllDecks() {
            return savedDecks;
        }

        // ==========================================
        // Create & Update (생성 및 수정)
        // ==========================================
        public async Task<string> CreateOrUpdateDeckAsync(string deckId, string deckName, List<int> cardIds) {
            string summary = GenerateDeckSummary(cardIds);
            Property repProp = CalculateRepresentativeProperty(cardIds); 

            DeckData existingDeck = GetDeck(deckId);
            DeckData targetDeck;

            bool isMySavedDeck = savedDecks.Contains(existingDeck);

            if (existingDeck != null && isMySavedDeck) {
                // 내 덱일 경우에만 덮어쓰기
                existingDeck.deckName = deckName;
                existingDeck.cardIds = new List<int>(cardIds);
                existingDeck.cardCountSummary = summary; 
                existingDeck.representativeProperty = repProp; 
                targetDeck = existingDeck;
            } else {
                // 프리셋 덱이거나 아예 없는 덱이면 무조건 내 덱 리스트에 새로 추가 (복사 효과)
                targetDeck = new DeckData(null, deckName, cardIds, summary, repProp); 
                savedDecks.Add(targetDeck);
            }

            await SaveDecksLocalAsync();
            return targetDeck.id;
        }
        
        // ==========================================
        // 덱 저장(생성/수정) 통합 API
        // ==========================================
        private async Task<bool> SaveDeckToServerAPI(DeckData deckToSave)
        {
            string serverURL = AuthManager.Instance.serverURL;
            string token = LocalDataManager.Instance.userToken;
            string userId = LocalDataManager.Instance.userId;

            if (string.IsNullOrEmpty(token)) {
                Debug.LogError("[DeckManager] 토큰이 없어 서버에 덱을 저장할 수 없습니다.");
                return false;
            }

            List<string> stringCardIds = deckToSave.cardIds.Select(id => id.ToString()).ToList();

            // 서버 API 규격(POST /decks)에 맞춘 DTO 생성
            ServerDeckDto requestDto = new ServerDeckDto
            {
                userId = userId,
                deckName = deckToSave.deckName, 
                cards = stringCardIds
            };

            string jsonData = JsonUtility.ToJson(requestDto);

            using (UnityWebRequest request = new UnityWebRequest(serverURL + "/decks", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
        
                request.SetRequestHeader("Content-Type", "application/json");
                // 인증 토큰
                request.SetRequestHeader("Authorization", "Bearer " + token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"<color=#00FF00>[DeckManager] 서버에 '{deckToSave.deckName}' 덱을 성공적으로 저장(동기화)했습니다.</color>");
                    return true;
                }
                else
                {
                    Debug.LogError($"[DeckManager] 덱 서버 저장 실패 (400/500 에러): {request.error} | {request.downloadHandler.text}");
                    return false;
                }
            }
        }
        
        
        // ==========================================
        // 덱 요약 문자열 생성 헬퍼 함수
        // ==========================================
        private string GenerateDeckSummary(List<int> cardIds) {
            if (cardIds == null || cardIds.Count == 0) {
                return "빈 덱";
            }

            // 속성별 개수를 카운트할 딕셔너리
            Dictionary<Property, int> propertyCounts = new Dictionary<Property, int>();

            foreach (int id in cardIds) {
                var card = CardDatabase.Instance.GetCardById(id);
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
        
        // Property Enum을 한글 문자열로 변환해주는 함수
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
        // Delete
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
        // 서버/로컬 통합 저장
        // ==========================================
        
        public async Task<bool> OnSaveDeckButtonClicked(string deckId)
        {
            DeckData targetDeck = GetDeck(deckId);

            if (targetDeck == null) {
                Debug.LogError("[DeckManager] 저장하려는 덱을 찾을 수 없습니다.");
                return false;
            }

            // 1. 방어 로직: 빈 덱 차단
            if (targetDeck.cardIds == null || targetDeck.cardIds.Count == 0) {
                CommonUIController.Instance.ShowBlackAlert("덱이 저장되었습니다.");

                Debug.LogWarning("[DeckManager] 카드가 없는 빈 덱은 서버에 저장할 수 없습니다.");
                return false;
            }

            // 2. 서버에 먼저 전송 시도
            bool isServerSuccess = await SaveDeckToServerAPI(targetDeck);
    
            if (isServerSuccess) {
                // 3. 서버 저장이 완벽하게 성공했을 때만! 내 휴대폰(로컬)에도 확정 도장을 찍어줍니다.
                // 이렇게 하면 서버 데이터와 로컬 데이터가 100% 일치하게 됩니다.
                await SaveDecksLocalAsync();
                CommonUIController.Instance.ShowBlackAlert("덱이 저장되었습니다.");
                Debug.Log("[DeckManager] 서버 및 로컬에 덱 동기화 저장이 완료되었습니다.");
                return true;
            } else {
                // 서버 저장이 실패하면 로컬 데이터도 덮어쓰지 않고 에러 처리
                CommonUIController.Instance.ShowRedAlert("서버 오류입니다. 다시 저장해주세요.");
                return false;
            }
        }
        
        private async Task SaveDecksLocalAsync() {
            DeckStorageWrapper wrapper = new DeckStorageWrapper { decks = this.savedDecks };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString("SavedDecks_Local", json);
            PlayerPrefs.Save();
            await Task.Yield();
        }

        // ==========================================
        // 덱 장착
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
        // 대표 속성 계산 로직
        // ==========================================
        private Property CalculateRepresentativeProperty(List<int> cardIds) {
            if (cardIds == null || cardIds.Count == 0) {
                return Property.None;
            }

            Dictionary<Property, int> counts = new Dictionary<Property, int>();
            Dictionary<Property, int> firstAppearance = new Dictionary<Property, int>();

            // 1. 순회하며 개수와 최초 등장 위치(Index) 기록
            for (int i = 0; i < cardIds.Count; i++) {
                var card = CardDatabase.Instance.GetCardById(cardIds[i]);
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
        
        /// <summary>
        /// 프리셋 덱을 유저의 덱 리스트에 복사하여 서버에 새로 저장합니다.
        /// </summary>
        public async void ClaimPresetDeck(PresetDeckData preset)
        {
            CommonUIController.Instance.ShowLoading();

            string newDeckId = ""; 
    
            // 프리셋의 이름과 카드 리스트를 그대로 넘겨서 내 덱으로 생성
            await DeckManager.Instance.CreateOrUpdateDeckAsync(newDeckId, preset.deckName, preset.cardIds);

            CommonUIController.Instance.DoneLoading();
            CommonUIController.Instance.ShowBlackAlert($"'{preset.deckName}'이(가) 내 덱에 추가되었습니다!");
    
            // TODO: DeckEditController의 좌측 덱 리스트 UI를 새로고침하는 함수 호출
        }
        
        
        public List<DeckData> GetAllPresetDecks() {
            List<DeckData> decks = new List<DeckData>();
            
            foreach (var preset in _presetDecks) {
                decks.Add( new DeckData(
                    preset.presetId,
                    preset.deckName,
                    preset.cardIds,
                    string.Empty,
                    preset.representativeProperty
                ) );
            }
            
            return decks;
        }
    }
}