using System;
using UnityEngine;
using UnityEngine.UI;
using Models.PlayerModels;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cards.CardUIDatas;
using Cards.EffectInfos;
using Cards.PlayableCards;
using Controllers.PlayerController;
using Controllers.SpellControllers;
using DefaultNamespace.Utilities;
using Models.CardDatabases;
using Models.TurnModel;
using UnityEngine.Pool;

namespace DefaultNamespace {
    
    public class PlayerUI : MonoBehaviour {
        public static PlayerUI Instance { get; private set; }


        [Header("체력 UI")] 
        public TextMeshProUGUI Text_Hp;
        public Slider Slider_Hp;

        [Header("마나 UI")] 
        public TextMeshProUGUI Text_Mana;
        public Image[] ManaSlots; // 하스스톤 스타일 마나 아이콘 10개 배열

        [Header("마나 색상 설정")] 
        public Color Color_ExpectedMana = Color.white; // 소모 예정 마나 (흰색)
        public Color Color_AvailableMana = new Color(0.4f, 0.8f, 1f); // 2. 사용 가능한 마나 (밝은 하늘색)
        public Color Color_UsedMana = new Color(0.1f, 0.3f, 0.5f); // 3. 이미 사용한 마나 (어두운 하늘색)
        public Color Color_LockedMana = Color.gray; // 1. 아직 도달하지 않은 최대 마나 (회색)

        [Header("상태이상 UI 설정")] 
        public Transform StatusGrid; // GridLayoutGroup이 붙은 상태이상 부모 객체
        public GameObject StatusIconPrefab; // StatusIcon.cs가 붙은 프리팹
        public StatusIconDatabase IconDatabase;

        [Header("손패 UI 설정")] 
        public Transform HandContainer;
        public GameObject CardPrefab;
        public Action<int> OnCardClickedAction;

        [Header("이전 속성")] 
        public Image img_LastElement;
        public TextMeshProUGUI txt_LastElement;
        
        [Header("우하단 버튼")]
        public Button btn_endTurn;
        public Button btn_spell;

        private IObjectPool<UI_Card_InHand> cardPool;
        private List<UI_Card_InHand> activeCards = new List<UI_Card_InHand>();

        private bool isDataBound = false;

        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // 풀 초기화
            cardPool = new ObjectPool<UI_Card_InHand>(
                createFunc: () => {
                    GameObject obj = Instantiate(CardPrefab, HandContainer);
                    return obj.GetComponent<UI_Card_InHand>();
                },
                actionOnGet: (card) => card.gameObject.SetActive(true),
                actionOnRelease: (card) => {
                    card.gameObject.SetActive(false);
                    card.transform.SetParent(HandContainer); // 풀 반환 시 부모 원상복구
                },
                actionOnDestroy: (card) => Destroy(card.gameObject),
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 20
            );
            
            isDataBound = false;
        }

        
        public void ReceiveData(PlayerController controller) 
        {
            // 🌟 2. 이미 연결을 마쳤다면, 아무 작업도 하지 않고 돌아가게 하여 중복 누적을 방어합니다.
            if (isDataBound) {
                Debug.Log("⚠️ [PlayerUI] 이미 데이터 바인딩이 완료되어 중복 실행을 차단했습니다.");
                return; 
            }
            isDataBound = true; // 문 걸어 잠그기
            
            PlayerModel model = controller.model;
            
            // 1. 체력 바인딩 (MaxHealth와 CurrentHealth 모두 추적)
            UpdateHealth(model.CurrentHealth.Value, model.MaxHealth.Value);
            model.CurrentHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(newValue, model.MaxHealth.Value);
            model.MaxHealth.OnValueChanged += (oldValue, newValue) => UpdateHealth(model.CurrentHealth.Value, newValue);

            // 2. 마나 바인딩 (Current, Max, Final 모두 추적)
            UpdateMana(model.CurrentMana.Value, model.MaxMana.Value, model.FinalMana.Value, model.ExpectedManaCost);
            model.CurrentMana.OnValueChanged += (oldValue, newValue) =>
                UpdateMana(newValue, model.MaxMana.Value, model.FinalMana.Value);
            model.MaxMana.OnValueChanged += (oldValue, newValue) =>
                UpdateMana(model.CurrentMana.Value, newValue, model.FinalMana.Value);
            model.OnExpectedManaChanged += (newExpectedMana) => 
                UpdateMana(model.CurrentMana.Value, model.MaxMana.Value, model.FinalMana.Value, newExpectedMana);
            model.LastProperty.OnValueChanged += (oldValue, newValue) => UpdateLastProperty(newValue);
            
            
            // 3. 기타 상태 및 카드 정보 바인딩
            UpdateStatuses(model.ActiveStatuses);
            model.ActiveStatuses.OnListChanged += (changeEvent) => UpdateStatuses(model.ActiveStatuses);

            if (model.Hand != null) {
                UpdateHandInfo(model.Hand.localHand);
                model.Hand.localHand.CollectionChanged += (sender, e) => UpdateHandInfo(model.Hand.localHand);
            }
            
            btn_endTurn.onClick.RemoveAllListeners();
            btn_spell.onClick.RemoveAllListeners();
            
            btn_endTurn.onClick.AddListener( controller.TryTurnEnd );
            btn_spell.onClick.AddListener( controller.SubmitSpellSelection );
            this.OnCardClickedAction += controller.ToggleSpellIndex;
        
            Debug.Log("✅ PlayerUI가 스스로 컨트롤러 데이터를 받아 바인딩을 완료했습니다!");
        }


        public void UpdateHealth(int currentHp, int maxHp) {
            Text_Hp.text = currentHp.ToString();
            if (Slider_Hp != null) {
                Slider_Hp.maxValue = maxHp;
                Slider_Hp.value = currentHp;
            }
        }

        public void UpdateMana(int currentMana, int maxMana, int finalMana, int expectedMana = 0) {
            Text_Mana.text = currentMana.ToString();

            if (ManaSlots == null || ManaSlots.Length == 0) return;

            // 소모 후 남을 진짜 가용 마나 계산
            int remainingMana = currentMana - expectedMana;

            for (int i = 0; i < ManaSlots.Length; i++) {
                if (i >= finalMana) {
                    ManaSlots[i].gameObject.SetActive(false);
                }
                else {
                    ManaSlots[i].gameObject.SetActive(true);

                    if (i < remainingMana) {
                        ManaSlots[i].color = Color_AvailableMana; // 아직 안 쓰고 남는 마나
                    }
                    else if (i < currentMana) {
                        ManaSlots[i].color = Color_ExpectedMana; // 이번에 선택한 카드로 인해 소모될 마나 (하얗게 하이라이트)
                    }
                    else if (i < maxMana) {
                        ManaSlots[i].color = Color_UsedMana; // 이미 소비해서 비어있는 마나
                    }
                    else {
                        ManaSlots[i].color = Color_LockedMana; // 아직 해금되지 않은 마나
                    }
                }
            }
        }


        public void UpdateStatuses(NetworkList<StatusData> statuses) {
            // 🌟 방어 1: 부모 객체(Grid)나 프리팹이 누락되었는지 확인
            if (StatusGrid == null || StatusIconPrefab == null) return;

            // 1. 기존에 생성된 아이콘들을 모두 지운다
            foreach (Transform child in StatusGrid) {
                Destroy(child.gameObject);
            }

            // 2. 스택 합산 로직
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
                    Debug.LogError("[PlayerUI] 🚨 StatusUIDataManager 인스턴스를 찾을 수 없습니다! 씬에 오브젝트가 있는지 확인해주세요.");
                    continue;
                }

                // 원래 사용하던 싱글톤 매니저 방식으로 복귀
                var uiData = StatusUIDataManager.Instance.GetStatusData(type);

                // 🌟 방어 3 (가장 유력한 범인): 매니저에 해당 상태이상 데이터가 등록 안 되어 있을 때
                if (uiData == null || uiData.Icon == null) {
                    Debug.LogWarning($"[PlayerUI] ⚠️ {type} 상태이상이 StatusUIDataManager에 등록되지 않았거나 아이콘이 누락되었습니다!");
                    continue; // 에러로 게임이 터지지 않고, 이 아이콘만 건너뜁니다.
                }

                GameObject iconObj = Instantiate(StatusIconPrefab, StatusGrid);
                UI_StatusIcon statusIcon = iconObj.GetComponent<UI_StatusIcon>();

                if (statusIcon != null) {
                    statusIcon.Setup(uiData.Icon, totalStacks);
                }
            }
        }
        
        public void UpdateLastProperty(Property prop) {
            var data = CardDatabase.Instance.GetElementData(prop);
            txt_LastElement.text = data.Name;
            img_LastElement.sprite = data.Icon;
        }

        private void UpdateHandInfo(ObservableCollection<int> localHand) {
            // 1. 화면에 있던 기존 카드들을 풀(Pool)로 모두 반납
            foreach (var card in activeCards) {
                cardPool.Release(card);
            }

            activeCards.Clear();

            // 2. 현재 손패 장수만큼 풀에서 카드를 꺼내와서 데이터 세팅
            for (int i = 0; i < localHand.Count; i++) {
                int cardId = localHand[i];
                var rawCardData = CardDatabase.Instance.GetCardById(cardId);

                PlayableCard genericCard = rawCardData;
                if (genericCard == null) continue;

                // 풀에서 가져오기
                UI_Card_InHand cardUI = cardPool.Get();

                // 손패 순서대로 그리기 위해 Hierarchy 인덱스를 맨 아래로 설정
                cardUI.transform.SetAsLastSibling();

                cardUI.Init(genericCard, i, (clickedIndex) => { OnCardClickedAction?.Invoke(clickedIndex); });

                activeCards.Add(cardUI);
            }

            // 3. 생성된 카드들을 부채꼴로 예쁘게 정렬하도록 매니저 호출
            if (HandLayoutManager.Instance != null) {
                HandLayoutManager.Instance.ArrangeCards(activeCards);
            }
        }
        
        public void ToggleCardHighlight(int index, bool isOn) {
            Debug.Log("진입"+index+isOn);
            // 안전망: 인덱스가 범위를 벗어나지 않았는지 체크
            if (index >= 0 && index < activeCards.Count)
            {
                activeCards[index].SetHighlight(isOn);
            }
        }
    }
}