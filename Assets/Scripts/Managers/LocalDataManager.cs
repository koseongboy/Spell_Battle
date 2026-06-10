using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;

namespace Managers.LocalDataManagers
{
    // ==========================================
    // 📦 디스크에 기록될 데이터의 형태 (직렬화 필수)
    // ==========================================
    [Serializable]
    public class UserSaveData
    {
        [Header("인증 정보 (API 호출 시 필요)")]
        public string userToken = string.Empty;
        public string userId = string.Empty;
        
        [Header("필수 플레이어 정보 (항상 들고 다니는 데이터)")]
        public string nickname = string.Empty;
        public int level = 1;
        public int gold = 0;
        public int selectedAvatarId = 0;
        
        // 추가된 게임 데이터 필드
        [Header("게임 플레이 정보")]
        public int score = 0;
        public string rank = string.Empty;
        public float defaultPitch = 150.0f;

        [Header("마이크 설정 세팅 값")]
        public int deviceIndex = 0;
        public float micVol = 0.5f;
        public float outVol = 0.5f;

        [Header("장착된 덱")]
        public List<int> equippedDeck = new List<int>();
    }
    public class LocalDataManager : MonoBehaviour
    {
        public static LocalDataManager Instance { get; private set; }
        [Header("현재 로컬 데이터")]
        public UserSaveData currentData = new UserSaveData();
        public string userToken {get => currentData.userToken; set => currentData.userToken = value;}
        public string userId {get => currentData.userId; set => currentData.userId = value;}
        public string nickname {get => currentData.nickname; set => currentData.nickname = value;}
        public int level {get => currentData.level; set => currentData.level = value;}
        public int gold {get => currentData.gold; set => currentData.gold = value;}
        public int selectedAvatarId {get => currentData.selectedAvatarId; set => currentData.selectedAvatarId = value;}
        public int score {get => currentData.score; set => currentData.score = value;}
        public string rank {get => currentData.rank; set => currentData.rank = value;}
        public float defaultPitch {get => currentData.defaultPitch; set => currentData.defaultPitch = value;}
        public int deviceIndex {get => currentData.deviceIndex; set => currentData.deviceIndex = value;}
        public float micVol {get => currentData.micVol; set => currentData.micVol = value;}
        public float outVol {get => currentData.outVol; set => currentData.outVol = value;}
        public List<int> equippedDeck
        {
            get { return currentData.equippedDeck; }
            set
            {
                currentData.equippedDeck = new List<int>(value);
                Debug.Log($"[LocalData] 덱 세팅 완료! 현재 {currentData.equippedDeck.Count}장");
                SaveData(); //덱 변경 시 마다 저장
            }
        }

        // OS 레벨의 물리적 파일 저장 경로
        private string saveFilePath;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                saveFilePath = Path.Combine(Application.persistentDataPath, "UserData.json");
 
                LoadData();
            }
            else Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SaveData();
            }
        }

        public void UpdateMicSetting(int idx, float micV, float outV)
        {
            deviceIndex = idx;
            micVol = micV;
            outVol = outV;
            SaveData();
        }
        public (int deviceIdx, float micV, float outV) GetMicSettings()
        {
            return (deviceIndex, micVol, outVol);
        }
        
        // 로그아웃 시 데이터를 초기화하는 함수
        public void ClearData()
        {
            userToken = string.Empty;
            userId = string.Empty;
            nickname = string.Empty;
            score = 0;
            rank = string.Empty;
            defaultPitch = 0f;
        }

        // ==========================================
        // 💾 1. 메모리의 데이터를 디스크(JSON 파일)로 쓰기
        // ==========================================
        public void SaveData()
        {
            if (string.IsNullOrEmpty(saveFilePath)) 
            {
                Debug.LogWarning("[LocalDataManager] 저장 경로가 비어있어 SaveData를 취소합니다.");
                return;
            }
            // 1. 객체를 JSON 형식의 텍스트로 변환 (true를 넣으면 들여쓰기/줄바꿈이 적용되어 사람이 읽기 편해집니다)
            string jsonText = JsonUtility.ToJson(currentData, true);

            // 2. 파일 스트림을 열어 텍스트 기록 (기존 파일이 있으면 덮어씁니다)
            File.WriteAllText(saveFilePath, jsonText);
            
            Debug.Log($"[LocalDataManager] 데이터 저장 완료! 경로: {saveFilePath}");
        }

        // ==========================================
        // 📂 2. 디스크에서 데이터를 읽어와 메모리에 적재
        // ==========================================
        public void LoadData()
        {
            if (File.Exists(saveFilePath))
            {
                // 1. 파일에서 텍스트 전체를 읽어옵니다.
                string jsonText = File.ReadAllText(saveFilePath);

                // 2. JSON 텍스트를 파싱하여 다시 객체(UserSaveData)로 메모리에 올립니다.
                currentData = JsonUtility.FromJson<UserSaveData>(jsonText);
                
                Debug.Log($"[LocalDataManager] 기존 저장 데이터를 성공적으로 불러왔습니다. 플레이어 토큰 {currentData.userToken}");
            }
            else
            {
                Debug.Log("[LocalDataManager] 저장된 파일이 없어 기본값으로 초기화합니다.");
                // 첫 실행이라 파일이 없다면 초기 기본값으로 빈 파일을 즉시 하나 생성해 줍니다.
                SaveData();
            }
        }
    }
}
