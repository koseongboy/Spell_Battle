using UnityEngine;
using UnityEngine.UI;
using Models.PlayerModels;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;

namespace DefaultNamespace
{
    public class EnemyUI : MonoBehaviour
    {
        public static EnemyUI Instance { get; private set; }

        [Header("체력 UI")]
        public TextMeshProUGUI Text_Hp;
        public Slider Slider_Hp;

        [Header("마나 UI")]
        public TextMeshProUGUI Text_Mana;
        public Image[] ManaSlots; 
        
        [Header("마나 색상 설정")]
        public Color Color_AvailableMana = new Color(0.4f, 0.8f, 1f);
        public Color Color_UsedMana = new Color(0.1f, 0.3f, 0.5f);
        public Color Color_LockedMana = Color.gray;

        [Header("상대 카드 및 상태이상 정보")]
        public TextMeshProUGUI Text_DeckCount;
        public TextMeshProUGUI Text_HandCount;
        
        [Header("상태이상 UI 설정")]
        public Transform StatusGrid;           // GridLayoutGroup이 붙은 상태이상 부모 객체
        public GameObject StatusIconPrefab;    // StatusIcon.cs가 붙은 프리팹
        public List<StatusIconMapping> StatusIconMappings; // 인스펙터에서 아이콘 할당

        // 매핑 리스트를 Dictionary로 변환해서 빠르게 찾기 위한 캐싱용
        private Dictionary<StatusType, Sprite> _iconDict = new Dictionary<StatusType, Sprite>();
        
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            // 리스트로 받은 매핑 정보를 Dictionary로 변환
            foreach (var mapping in StatusIconMappings)
            {
                if (!_iconDict.ContainsKey(mapping.Type))
                    _iconDict.Add(mapping.Type, mapping.IconSprite);
            }
        }

        public void Bind(PlayerModel model)
        {
            UpdateHealth(model.CurrentHealth.Value, model.MaxHealth.Value);
            model.CurrentHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(newValue, model.MaxHealth.Value);
            model.MaxHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(model.CurrentHealth.Value, newValue);

            UpdateMana(model.CurrentMana.Value, model.MaxMana.Value, model.FinalMana.Value);
            model.CurrentMana.OnValueChanged += (oldValue, newValue) => UpdateMana(newValue, model.MaxMana.Value, model.FinalMana.Value);
            model.MaxMana.OnValueChanged += (oldValue, newValue) => UpdateMana(model.CurrentMana.Value, newValue, model.FinalMana.Value);

            UpdateStatuses(model.ActiveStatuses);
            model.ActiveStatuses.OnListChanged += (changeEvent) => UpdateStatuses(model.ActiveStatuses);

            if (model.Deck != null)
            {
                UpdateDeckCount(model.Deck.DeckCount.Value); 
                model.Deck.DeckCount.OnValueChanged += (oldValue, newValue) => UpdateDeckCount(newValue);
            }

            if (model.Hand != null)
            {
                UpdateHandCount(model.Hand.HandCount.Value);
                model.Hand.HandCount.OnValueChanged += (oldValue, newValue) => UpdateHandCount(newValue);
            }
        }

        private void UpdateHealth(int currentHp, int maxHp)
        {
            Text_Hp.text = $"적 HP: {currentHp} / {maxHp}";
            if (Slider_Hp != null)
            {
                Slider_Hp.maxValue = maxHp;
                Slider_Hp.value = currentHp;
            }
        }

        private void UpdateMana(int currentMana, int maxMana, int finalMana)
        {
            Text_Mana.text = $"적 MP: {currentMana} / {maxMana}";

            if (ManaSlots == null || ManaSlots.Length == 0) return;

            for (int i = 0; i < ManaSlots.Length; i++)
            {
                if (i >= finalMana)
                {
                    ManaSlots[i].gameObject.SetActive(false);
                }
                else
                {
                    ManaSlots[i].gameObject.SetActive(true);

                    if (i < currentMana) ManaSlots[i].color = Color_AvailableMana;
                    else if (i < maxMana) ManaSlots[i].color = Color_UsedMana;
                    else ManaSlots[i].color = Color_LockedMana;
                }
            }
        }

        private void UpdateDeckCount(int count) => Text_DeckCount.text = $"적 덱: {count}장";
        private void UpdateHandCount(int count) => Text_HandCount.text = $"적 손패: {count}장";
        
        private void UpdateStatuses(NetworkList<StatusData> statuses)
        {
            // 1. 기존에 생성된 아이콘들을 모두 지운다 (오브젝트 풀링을 쓰면 더 좋지만 우선 Destroy로 구현)
            foreach (Transform child in StatusGrid)
            {
                Destroy(child.gameObject);
            }

            // 2. 여러 개로 나뉘어 있을 수 있는 상태이상을 타입별로 합산 (예: 발화 1 + 발화 2 = 발화 3)
            Dictionary<StatusType, int> displayStatusTotals = new Dictionary<StatusType, int>();

            foreach (var status in statuses)
            {
                if (displayStatusTotals.ContainsKey(status.Type))
                {
                    displayStatusTotals[status.Type] += status.Stacks;
                }
                else
                {
                    displayStatusTotals[status.Type] = status.Stacks;
                }
            }

            // 3. 합산된 데이터를 바탕으로 프리팹 생성 및 설정
            foreach (var kvp in displayStatusTotals)
            {
                StatusType type = kvp.Key;
                int totalStacks = kvp.Value;

                // 스택이 0 이하라면 표시하지 않음
                if (totalStacks <= 0) continue;

                // 매핑된 아이콘 이미지가 있는지 확인
                Sprite iconSprite = _iconDict.ContainsKey(type) ? _iconDict[type] : null;

                // 프리팹 생성 후 세팅
                GameObject iconObj = Instantiate(StatusIconPrefab, StatusGrid);
                UI_StatusIcon statusIcon = iconObj.GetComponent<UI_StatusIcon>();
                
                if (statusIcon != null)
                {
                    statusIcon.Setup(iconSprite, totalStacks);
                }
            }
        }
    }
}