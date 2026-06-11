using UnityEngine;
using UnityEngine.UI;
using Models.PlayerModels;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using Controllers.PlayerController;

namespace DefaultNamespace {
    public class EnemyUI : MonoBehaviour {
        #region Fields

        public static EnemyUI Instance { get; private set; }

        [Header("체력 UI")] public TextMeshProUGUI Text_Hp;
        public Slider Slider_Hp;

        [Header("마나 UI")] public TextMeshProUGUI Text_Mana;

        [Header("상대 카드 및 상태이상 정보")] public TextMeshProUGUI Text_DeckCount;
        public TextMeshProUGUI Text_HandCount;

        [Header("상태이상 UI 설정")] public Transform StatusGrid;
        public GameObject StatusIconPrefab;
        public StatusIconDatabase IconDatabase;

        private bool isDataBound = false;
        private PlayerModel model;

        #endregion

        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            isDataBound = false;
        }

        public void ReceiveData(PlayerController controller) {
            if (isDataBound) return;
            isDataBound = true;

            model = controller.model;

            // 체력 바인딩
            UpdateHealth(model.CurrentHealth.Value, model.MaxHealth.Value);
            model.CurrentHealth.OnValueChanged += HandleHealthChanged;
            model.MaxHealth.OnValueChanged += HandleMaxHealthChanged;

            // 마나 바인딩
            UpdateMana(model.CurrentMana.Value, model.MaxMana.Value);
            model.CurrentMana.OnValueChanged += HandleCurrentManaChanged;
            model.MaxMana.OnValueChanged += HandleMaxManaChanged;

            // 상태이상 바인딩
            UpdateStatuses(model.ActiveStatuses);
            model.ActiveStatuses.OnListChanged += HandleStatusChanged;

            // 덱 & 손패 카운트 바인딩
            UpdateDeckCount(model.Deck.DeckCount.Value);
            model.Deck.DeckCount.OnValueChanged += HandleDeckCountChanged;

            UpdateHandCount(model.Hand.HandCount.Value);
            model.Hand.HandCount.OnValueChanged += HandleHandCountChanged;
        }

        private void OnEnable() {
            if (model != null) {
                UpdateHealth(model.CurrentHealth.Value, model.MaxHealth.Value);
                UpdateMana(model.CurrentMana.Value, model.MaxMana.Value);
                UpdateStatuses(model.ActiveStatuses);

                if (model.Deck != null) UpdateDeckCount(model.Deck.DeckCount.Value);
                if (model.Hand != null) UpdateHandCount(model.Hand.HandCount.Value);
            }
        }

        private void OnDestroy() {
            if (model != null) {
                model.CurrentHealth.OnValueChanged -= HandleHealthChanged;
                model.MaxHealth.OnValueChanged -= HandleMaxHealthChanged;

                model.CurrentMana.OnValueChanged -= HandleCurrentManaChanged;
                model.MaxMana.OnValueChanged -= HandleMaxManaChanged;

                model.ActiveStatuses.OnListChanged -= HandleStatusChanged;

                model.Deck.DeckCount.OnValueChanged -= HandleDeckCountChanged;
                model.Hand.HandCount.OnValueChanged -= HandleHandCountChanged;

                model = null;
            }

            isDataBound = false;
        }


        #region Update UI

        private void UpdateHealth(int currentHp, int maxHp) {
            Text_Hp.text = currentHp.ToString();
            if (Slider_Hp != null) {
                Slider_Hp.maxValue = maxHp;
                Slider_Hp.value = currentHp;
            }
        }

        private void UpdateMana(int currentMana, int maxMana) {
            Text_Mana.text = $"{currentMana} / {maxMana}";
        }

        private void UpdateDeckCount(int count) => Text_DeckCount.text = count.ToString();
        private void UpdateHandCount(int count) => Text_HandCount.text = count.ToString();

        public void UpdateStatuses(NetworkList<StatusData> statuses) {
            // 🌟 방어 1: 부모 객체(Grid)나 프리팹이 인스펙터에서 누락되었는지 확인
            if (StatusGrid == null || StatusIconPrefab == null) return;

            // 1. 기존 아이콘 초기화
            foreach (Transform child in StatusGrid) {
                Destroy(child.gameObject);
            }

            // 2. 스택 합산 로직 (기존과 동일)
            Dictionary<StatusType, int> displayStatusTotals = new Dictionary<StatusType, int>();
            foreach (var status in statuses) {
                if (displayStatusTotals.ContainsKey(status.Type))
                    displayStatusTotals[status.Type] += status.Stacks;
                else
                    displayStatusTotals[status.Type] = status.Stacks;
            }

            // 3. 아이콘 생성 로직
            foreach (var kvp in displayStatusTotals) {
                StatusType type = kvp.Key;
                int totalStacks = kvp.Value;

                if (totalStacks <= 0) continue;

                // 🌟 방어 2: 매니저 싱글톤 자체가 씬에 없는 경우 체크
                if (StatusUIDataManager.Instance == null) {
                    Debug.LogError("[EnemyUI] 🚨 StatusUIDataManager 인스턴스를 찾을 수 없습니다! 씬에 오브젝트가 배치되어 있는지 확인해주세요.");
                    continue;
                }

                // 매니저에서 상태이상 UI 데이터 가져오기
                var uiData = StatusUIDataManager.Instance.GetStatusData(type);

                // 🌟 방어 3 (핵심 범인 차단): 데이터가 없거나 아이콘 이미지가 등록 안 되어 있을 때
                if (uiData == null || uiData.Icon == null) {
                    Debug.LogWarning($"[EnemyUI] ⚠️ {type} 상태이상이 StatusUIDataManager에 등록되지 않았거나 아이콘이 누락되었습니다!");
                    continue; // 에러로 게임이 터지지 않고, 이 아이콘만 건너뜁니다.
                }

                GameObject iconObj = Instantiate(StatusIconPrefab, StatusGrid);
                UI_StatusIcon statusIcon = iconObj.GetComponent<UI_StatusIcon>();

                if (statusIcon != null) {
                    statusIcon.Setup(uiData.Icon, totalStacks);
                }
            }
        }

        #endregion


        #region 값 변화 handler

        private void HandleHealthChanged(int oldValue, int newValue) {
            if (model != null) UpdateHealth(newValue, model.MaxHealth.Value);
        }

        private void HandleMaxHealthChanged(int oldValue, int newValue) {
            if (model != null) UpdateHealth(model.CurrentHealth.Value, newValue);
        }

        private void HandleCurrentManaChanged(int oldValue, int newValue) {
            if (model != null) UpdateMana(newValue, model.MaxMana.Value);
        }

        private void HandleMaxManaChanged(int oldValue, int newValue) {
            if (model != null) UpdateMana(model.CurrentMana.Value, newValue);
        }

        private void HandleStatusChanged(Unity.Netcode.NetworkListEvent<StatusData> changeEvent) {
            if (model != null) UpdateStatuses(model.ActiveStatuses);
        }

        private void HandleDeckCountChanged(int oldValue, int newValue) {
            UpdateDeckCount(newValue);
        }

        private void HandleHandCountChanged(int oldValue, int newValue) {
            UpdateHandCount(newValue);
        }

        #endregion
    }
}