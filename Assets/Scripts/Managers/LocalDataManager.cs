using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Managers.LocalDataManagers
{
    public class LocalDataManager : MonoBehaviour
    {
        public static LocalDataManager Instance { get; private set; }

        [Header("인증 정보 (API 호출 시 필요)")]
        public string userToken = "";

        [Header("필수 플레이어 정보 (항상 들고 다니는 데이터)")]
        public string nickname = "비로그인맨";
        public int level = 1;
        public int gold = 0;
        public int selectedAvatarId = 0;

        [Header("마이크 설정 세팅 값")]
        public int deviceIndex;
        public float micVol;
        public float outVol;

        [SerializeField]private List<int> _equippedDeck = new List<int>();
        public List<int> equippedDeck
        {
            get { return _equippedDeck; }
            set
            {
                _equippedDeck = new List<int>(value);
                Debug.Log($"[LocalData] 덱 세팅 완료! 현재 {_equippedDeck.Count}장");
            }
        }
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        public async Task LoadInitialPlayerDataAsync(string token)
        {
            this.userToken = token;
            Debug.Log("🌐 서버에서 필수 플레이어 정보를 불러옵니다...");

            // ----------------------------------------------------
            // 💡 TODO: [웹 서버 통신 위치 - 필수 데이터 요청]
            // 여기서 UnityWebRequest나 HttpClient를 사용해 웹 서버(REST API)를 호출하세요.
            // 응답으로 받은 JSON 데이터를 파싱해서 아래 변수들에 채워 넣으면 됩니다.
            // ----------------------------------------------------
            
            // [임시 하드코딩 - 통신 구현 후 지워주세요]
            await Task.Delay(500); // 통신 딜레이 흉내
            this.nickname = "TestUser";
            this.level = 10;
            this.gold = 1500;
            this.selectedAvatarId = 101;
            // ----------------------------------------------------

            Debug.Log($"✅ 데이터 로드 완료! 환영합니다, {this.nickname}님.");
        }

        // ==========================================
        // 📡 [B. 지연 로딩(Lazy Loading)] 특정 UI를 열었을 때만 호출!
        // ==========================================
        public async Task<string> FetchMatchHistoryAsync()
        {
            Debug.Log("🌐 서버에 전적 데이터를 요청합니다...");

            // ----------------------------------------------------
            // 💡 TODO: [웹 서버 통신 위치 - 부가 데이터 요청]
            // 전적 보기 창, 상점 창 등을 열었을 때만 이 함수들을 호출하세요.
            // this.userToken을 헤더나 바디에 담아 요청하여 데이터를 받아옵니다.
            // ----------------------------------------------------

            await Task.Delay(300); 
            return "최근 전적 데이터 JSON 문자열 (또는 파싱된 객체)";
        }

        public void UpdateMicSetting(int idx, float micV, float outV)
        {
            deviceIndex = idx;
            micVol = micV;
            outVol = outV;
        }
        public (int deviceIdx, float micV, float outV) GetMicSettings()
        {
            return (deviceIndex, micVol, outVol);
        }
    }
}
