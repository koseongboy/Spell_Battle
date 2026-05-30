using UnityEngine;
using Controllers.PlayerController;
using Models.PlayerModels;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering.LookDev;
// using TMPro;

namespace Views.PlayerView // 기존에 쓰시던 네임스페이스 그대로 사용!
{
    public class PlayerView : MonoBehaviour
    {
        // 씬에서 컨트롤러가 이 View를 쉽게 찾을 수 있도록 싱글톤 처리
        public static PlayerView Instance { get; private set; }
        [Header("내 캐릭터 정보")]
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
        // 🔌 플레이어가 태어나면 모델(Model)과 뷰를 연결하는 함수
        // ==========================================
        public void Bind(PlayerModel model)
        {
            // 1. 초기값 세팅 (스폰 직후 현재 상태 반영)
            UpdateHealth(model.CurrentHealth.Value);
            UpdateMana(model.CurrentMana.Value);
            UpdateStatuses(model.ActiveStatuses);

            // 2. 데이터 변경 구독 (NetworkVariable의 OnValueChanged 활용)
            model.CurrentHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(newValue);
            model.CurrentMana.OnValueChanged += (oldValue, newValue) => UpdateMana(newValue);
            
            // 상태이상 리스트 구독
            model.ActiveStatuses.OnListChanged += (changeEvent) => UpdateStatuses(model.ActiveStatuses);
            
            Debug.Log("✅ [PlayerView] 내 캐릭터 모델과 UI가 성공적으로 연결(Bind)되었습니다.");
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