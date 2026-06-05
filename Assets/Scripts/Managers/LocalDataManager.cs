using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;

namespace Managers.LocalDataManagers
{
    public class LocalDataManager : MonoBehaviour
    {
        public static LocalDataManager Instance { get; private set; }

        [Header("인증 정보 (API 호출 시 필요)")]
        public string userToken = "";
        public string userId = "";
        
        [Header("필수 플레이어 정보 (항상 들고 다니는 데이터)")]
        public string nickname = "비로그인맨";
        public int level = 1;
        public int gold = 0;
        public int selectedAvatarId = 0;
        
        // 추가된 게임 데이터 필드
        [Header("게임 플레이 정보")]
        public int score = 0;
        public string rank = "Bronze";
        public float defaultPitch = 150.0f;

        [Header("마이크 설정 세팅 값")]
        public int deviceIndex = 0;
        public float micVol = 0.5f;
        public float outVol = 0.5f;

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
        
        // 로그아웃 시 데이터를 초기화하는 함수
        public void ClearData()
        {
            userToken = "";
            userId = "";
            nickname = "비로그인맨";
            score = 0;
            rank = "Bronze";
            defaultPitch = 150.0f;
        }
    }
}
