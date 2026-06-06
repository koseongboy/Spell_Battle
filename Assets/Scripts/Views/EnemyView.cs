using UnityEngine;
using Models.PlayerModels;
using Unity.Netcode;
using System.Collections.Generic;
using TMPro;
using Cards.CardUIDatas;
using DefaultNamespace;
using DefaultNamespace.Utilities;

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
        public TextMeshProUGUI Text_Shield;
        public TextMeshProUGUI Text_LastProperty;
        public TextMeshProUGUI Text_Status;
        public TextMeshProUGUI Text_HandCount;
        public TextMeshProUGUI Text_DeckCount;
        public TextMeshProUGUI Text_GraveCount;


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
            UpdateShield(enemyModel.Shield.Value);
            UpdateStatuses(enemyModel.ActiveStatuses);
            UpdateLastProperty(Property.None);
            if (enemyModel.Hand != null)
            {
                // 초기 적군 손패 장수 반영
                UpdateEnemyCardCount(enemyModel.Hand.HandCount.Value);

                // 상대방 손패 장수가 변할 때마다 텍스트 갱신 구독
                enemyModel.Hand.HandCount.OnValueChanged += (oldValue, newValue) => UpdateEnemyCardCount(newValue);
            }
            if (enemyModel.Deck != null)
            {
                // 초기값 세팅
                UpdateDeckCount(enemyModel.Deck.DeckCount.Value); 
                
                // 덱 장수가 변할 때마다 UI 자동 갱신
                enemyModel.Deck.DeckCount.OnValueChanged += (oldValue, newValue) => UpdateDeckCount(newValue);
            }

            // ==========================================
            // 🪦 무덤 장수 동기화
            // ==========================================
            if (enemyModel.Graveyard != null)
            {
                UpdateGraveyardCount(enemyModel.Graveyard.PublicGraveyard.Count);
                enemyModel.Graveyard.PublicGraveyard.OnListChanged += (changeEvent) => UpdateGraveyardCount(enemyModel.Graveyard.PublicGraveyard.Count);
            }

            // 2. 데이터 변경 구독 (적군의 데이터가 변하면 내 화면이 반응하도록 세팅)
            enemyModel.CurrentHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(newValue);
            enemyModel.CurrentMana.OnValueChanged += (oldValue, newValue) => UpdateMana(newValue);
            enemyModel.Shield.OnValueChanged += (oldValue, newValue) => UpdateShield(newValue);
            enemyModel.LastProperty.OnValueChanged += (oldValue, newValue) => UpdateLastProperty(newValue);
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
        public void UpdateShield(int currentShield)
        {
            Debug.Log("쉴드 생성됨: " + currentShield);
            Text_Shield.text = $"보호막: {currentShield}";
        }
        public  void UpdateStatuses(NetworkList<StatusData> statuses)
        {
            Debug.Log("상태이상이 변경됐습니다.");
            List<string> statusStrings = new List<string>();
            foreach(StatusData status in statuses)
            {
                string durationStr = status.Duration == -1 ? "영구" : $"{status.Duration}턴";
                statusStrings.Add($"{StatusUIDataManager.Instance.GetStatusData( status.Type ).name} [{status.Stacks}스택 / {durationStr}], ");
            }
            string finalMsg = string.Join(", ", statusStrings);
            Text_Status.text = "상태이상: " + finalMsg;
        }
        public void UpdateEnemyCardCount(int count)
        {
            Text_HandCount.text = $"패: {count}장";
            Debug.Log($"😈 적군 손패 장수 UI 갱신: {count}장");
        }
        public void UpdateDeckCount(int count)
        {
            Text_DeckCount.text = $"덱: {count}장";
            Debug.Log($"📚 덱 장수 UI 갱신: {count}장");
        }
        public void UpdateLastProperty(Property prop) // todo: 추후 맞는 이미지 적용하는 코드로 수정해야할 듯.
        {
            string prop_text = "";
            switch(prop)
            {
                case Property.Attack: prop_text = "공격"; break;
                case Property.Deffense: prop_text = "방어"; break;
                case Property.Fire: prop_text = "불"; break;
                case Property.Water: prop_text = "물"; break;
                case Property.Ground: prop_text = "흙"; break;
                case Property.Wind: prop_text = "바람"; break;
                case Property.Thunder: prop_text = "번개"; break;
                case Property.Ice: prop_text = "얼음"; break;
                case Property.Void: prop_text = "공허"; break;
                case Property.Vision: prop_text = "비전"; break;
                case Property.Life: prop_text = "생명"; break;
                case Property.None: prop_text = "없음"; break;
                default: prop_text = "(알 수 없음)"; break;
            }
            Text_LastProperty.text = $"마지막 속성: {prop_text}";
        }

        public void UpdateGraveyardCount(int count)
        {
            Text_GraveCount.text = $"무덤: {count}장";
            Debug.Log($"🪦 무덤 장수 UI 갱신: {count}장");
        }
        
    }
}