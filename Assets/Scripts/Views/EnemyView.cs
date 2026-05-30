using UnityEngine;
using Models.PlayerModels;
using Unity.Netcode;
using System.Collections.Generic;
using TMPro;

namespace Views.EnemyView 
{
    public class EnemyView : MonoBehaviour
    {
        // 씬에서 컨트롤러가 이 View를 찾을 수 있도록 싱글톤 처리
        public static EnemyView Instance { get; private set; }
        [Header("적 캐릭터 정보")]
        public TextMeshProUGUI Text_Name;
        public TextMeshProUGUI Text_Hp;
        public TextMeshProUGUI Text_Mana;
        public TextMeshProUGUI Text_Status;
        public TextMeshProUGUI Text_CardCount;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // ==========================================
        // 🔌 적군(복제본) 모델과 내 화면 상단의 적군 UI를 연결하는 함수
        // ==========================================
        public void Bind(PlayerModel enemyModel)
        {
            // 1. 초기값 세팅 (스폰 직후 현재 상태 반영)
            UpdateHealth(enemyModel.CurrentHealth.Value);
            UpdateMana(enemyModel.CurrentMana.Value);
            UpdateStatuses(enemyModel.ActiveStatuses);

            // 2. 데이터 변경 구독 (적군의 데이터가 변하면 내 화면이 반응하도록 세팅)
            enemyModel.CurrentHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(newValue);
            enemyModel.CurrentMana.OnValueChanged += (oldValue, newValue) => UpdateMana(newValue);
            enemyModel.ActiveStatuses.OnListChanged += (changeEvent) => UpdateStatuses(enemyModel.ActiveStatuses);
            
            Debug.Log("✅ [EnemyView] 적군 캐릭터 모델과 적군 UI가 성공적으로 연결(Bind)되었습니다.");
        }

        // ==========================================
        // 🎨 실제 화면 갱신 로직 (기존에 짜두셨던 코드 활용)
        // ==========================================
        public  void UpdateHealth(int currentHp)
        {
            Debug.Log("채력 설정 완료: " + currentHp);
            Text_Hp.text = $"HP: {currentHp}";
        }

        public void UpdateMana(int currentMana)
        {
            Debug.Log("마나 설정 완료: " + currentMana);
            Text_Mana.text = $"MP: {currentMana}";
        }
        public  void UpdateStatuses(NetworkList<StatusData> statuses)
        {
            Debug.Log("상태이상이 변경됐습니다.");
            List<string> statusStrings = new List<string>();
            foreach(StatusData status in statuses)
            {
                string durationStr = status.Duration == -1 ? "영구" : $"{status.Duration}턴";
                statusStrings.Add($"{status.GetTranslateStatus()} [{status.Stacks}스택 / {durationStr}], ");
            }
            string finalMsg = string.Join(", ", statusStrings);
            Text_Status.text = "상태이상: " + finalMsg;
        }

        
    }
}