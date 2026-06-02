using UnityEngine;
using UnityEngine.UI;
using Models.PlayerModels;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using Models.CardDatabases;

namespace DefaultNamespace
{
    // 인스펙터에서 상태이상 종류별로 이미지를 매핑하기 위한 구조체
    [System.Serializable]
    public struct StatusIconMapping
    {
        public StatusType Type;
        public Sprite IconSprite;
    }
    
    public class PlayerUI : MonoBehaviour
    {
        public static PlayerUI Instance { get; private set; }

        
        [Header("체력 UI")]
        public TextMeshProUGUI Text_Hp;

        public Slider Slider_Hp;

        [Header("마나 UI")]
        public TextMeshProUGUI Text_Mana;
        public Image[] ManaSlots; // 하스스톤 스타일 마나 아이콘 10개 배열
        
        [Header("마나 색상 설정")]
        public Color Color_AvailableMana = new Color(0.4f, 0.8f, 1f); // 2. 사용 가능한 마나 (밝은 하늘색)
        public Color Color_UsedMana = new Color(0.1f, 0.3f, 0.5f);    // 3. 이미 사용한 마나 (어두운 하늘색)
        public Color Color_LockedMana = Color.gray;                   // 1. 아직 도달하지 않은 최대 마나 (회색)
        
        [Header("상태이상 UI 설정")]
        public Transform StatusGrid;           // GridLayoutGroup이 붙은 상태이상 부모 객체
        public GameObject StatusIconPrefab;    // StatusIcon.cs가 붙은 프리팹
        public StatusIconDatabase IconDatabase;
        
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Bind(PlayerModel model)
        {
            // 1. 체력 바인딩 (MaxHealth와 CurrentHealth 모두 추적)
            UpdateHealth(model.CurrentHealth.Value, model.MaxHealth.Value);
            model.CurrentHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(newValue, model.MaxHealth.Value);
            model.MaxHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(model.CurrentHealth.Value, newValue);

            // 2. 마나 바인딩 (Current, Max, Final 모두 추적)
            UpdateMana(model.CurrentMana.Value, model.MaxMana.Value, model.FinalMana.Value);
            model.CurrentMana.OnValueChanged += (oldValue, newValue) => UpdateMana(newValue, model.MaxMana.Value, model.FinalMana.Value);
            model.MaxMana.OnValueChanged += (oldValue, newValue) => UpdateMana(model.CurrentMana.Value, newValue, model.FinalMana.Value);

            // 3. 기타 상태 및 카드 정보 바인딩
            UpdateStatuses(model.ActiveStatuses);
            model.ActiveStatuses.OnListChanged += (changeEvent) => UpdateStatuses(model.ActiveStatuses);

            if (model.Hand != null)
            {
                UpdateHandInfo(model.Hand.localHand);
                model.Hand.localHand.CollectionChanged += (sender, e) => UpdateHandInfo(model.Hand.localHand);
            }
        }

        public void UpdateHealth(int currentHp, int maxHp)
        {
            Text_Hp.text = currentHp.ToString();
            if (Slider_Hp != null)
            {
                Slider_Hp.maxValue = maxHp;
                Slider_Hp.value = currentHp;
            }
        }

        public void UpdateMana(int currentMana, int maxMana, int finalMana)
        {
            Text_Mana.text = currentMana.ToString();

            if (ManaSlots == null || ManaSlots.Length == 0) return;

            for (int i = 0; i < ManaSlots.Length; i++)
            {
                if (i >= finalMana)
                {
                    // 최대 한계치(FinalMana)를 넘어가는 슬롯은 아예 숨김 처리
                    ManaSlots[i].gameObject.SetActive(false);
                }
                else
                {
                    ManaSlots[i].gameObject.SetActive(true);

                    if (i < currentMana)
                    {
                        ManaSlots[i].color = Color_AvailableMana; // 현재 사용 가능한 마나
                    }
                    else if (i < maxMana)
                    {
                        ManaSlots[i].color = Color_UsedMana;      // 이번 턴에 이미 사용한 마나
                    }
                    else
                    {
                        ManaSlots[i].color = Color_LockedMana;    // 아직 해금되지 않은 마나 슬롯
                    }
                }
            }
        }


        public void UpdateStatuses(NetworkList<StatusData> statuses)
        {
            // 1. 기존에 생성된 아이콘들을 모두 지운다 (오브젝트 풀링을 쓰면 더 좋지만 우선 Destroy로 구현)
            foreach (Transform child in StatusGrid)
            {
                Destroy(child.gameObject);
            }

            // 2. 스택 합산 로직 (기존과 동일)
            Dictionary<StatusType, int> displayStatusTotals = new Dictionary<StatusType, int>();
            foreach (var status in statuses)
            {
                if (displayStatusTotals.ContainsKey(status.Type))
                    displayStatusTotals[status.Type] += status.Stacks;
                else
                    displayStatusTotals[status.Type] = status.Stacks;
            }

            // 3. 아이콘 생성 로직
            foreach (var kvp in displayStatusTotals)
            {
                StatusType type = kvp.Key;
                int totalStacks = kvp.Value;

                if (totalStacks <= 0) continue;
                
                Sprite iconSprite = IconDatabase != null ? IconDatabase.GetIcon(type) : null;

                GameObject iconObj = Instantiate(StatusIconPrefab, StatusGrid);
                UI_StatusIcon statusIcon = iconObj.GetComponent<UI_StatusIcon>();
                
                if (statusIcon != null)
                {
                    statusIcon.Setup(iconSprite, totalStacks);
                }
            }
        }

        private void UpdateHandInfo(System.Collections.ObjectModel.ObservableCollection<int> localHand)
        {
            List<string> cardNames = new List<string>();
            foreach (int cardId in localHand)
            {
                var cardData = CardDatabase.GetCardById(cardId);
                string name = (cardData != null && cardData.uiData != null) ? cardData.uiData.wordName : $"카드({cardId})";
                cardNames.Add(name);
            }
        }
    }
}