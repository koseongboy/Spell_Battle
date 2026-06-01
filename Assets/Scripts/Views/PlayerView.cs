using UnityEngine;
using Controllers.PlayerController;
using Models.PlayerModels;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
// using UnityEngine.Rendering.LookDev;
using Models.CardDatabases;
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
        public TextMeshProUGUI Text_HandCount;
        public TextMeshProUGUI Text_DeckCount;
        public TextMeshProUGUI Text_GraveCount;

        public TextMeshProUGUI Text_HandInfo;

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

            if (model.Hand != null)
            {
                // 게임 최초 진입 시 손패 화면 갱신
                UpdateHandUI(model.Hand.localHand, model.Hand.HandCount.Value);

                // 손패 리스트에 추가/삭제가 일어날 때마다 자동으로 실행되도록 구독!
                model.Hand.localHand.CollectionChanged += (sender, e) => UpdateHandUI(model.Hand.localHand, model.Hand.HandCount.Value);
            }

            if (model.Deck != null)
            {
                // 초기값 세팅
                UpdateDeckCount(model.Deck.DeckCount.Value); 
                
                // 덱 장수가 변할 때마다 UI 자동 갱신
                model.Deck.DeckCount.OnValueChanged += (oldValue, newValue) => UpdateDeckCount(newValue);
            }

            // ==========================================
            // 🪦 무덤 장수 동기화
            // ==========================================
            if (model.Graveyard != null)
            {

                UpdateGraveyardCount(model.Graveyard.PublicGraveyard.Count);
                model.Graveyard.PublicGraveyard.OnListChanged += (changeEvent) => UpdateGraveyardCount(model.Graveyard.PublicGraveyard.Count);
                
            }
            
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

        public void UpdateDeckCount(int count)
        {
            Text_DeckCount.text = $"덱: {count}장";
            Debug.Log($"📚 덱 장수 UI 갱신: {count}장");
        }

        public void UpdateGraveyardCount(int count)
        {
            Text_GraveCount.text = $"무덤: {count}장";
            Debug.Log($"🪦 무덤 장수 UI 갱신: {count}장");
        }

        public void UpdateHandUI(System.Collections.ObjectModel.ObservableCollection<int> localHand, int handCount)
        {
            if (localHand.Count == 0)
            {
                Text_HandInfo.text = "손패: 없음";
                return;
            }

            System.Collections.Generic.List<string> cardNames = new System.Collections.Generic.List<string>();

            // 손패에 든 카드 ID를 하나씩 꺼내서 이름 문자열로 변환
            foreach (int cardId in localHand)
            {
                cardNames.Add(ConvertIdToCardName(cardId));
            }

            // string.Join을 사용해 "화염(4001), 공격(1005), 방어(2012)" 형태로 결합
            string finalHandText = string.Join(", ", cardNames);
            Text_HandInfo.text = $"{finalHandText}";
            Text_HandCount.text = $"손패: {handCount}장";
            
            Debug.Log($"🃏 내 손패 UI 업데이트 완료: {finalHandText}");
        }

        private string ConvertIdToCardName(int id)
        {
            // 2. 이미 TurnController에서 검증용으로 사용 중인 데이터베이스 함수를 호출합니다.
            var cardData = CardDatabase.GetCardById(id); 

            if (cardData != null)
            {
                // 3. 현재 프로젝트의 카드 데이터 구조에 맞춰 이름을 반환합니다.
                // TurnController 구조상 cardData 내부 혹은 uiData 내부에 이름 변수가 있을 것입니다.
                // 만약 에러가 난다면 프로젝트의 구조에 따라 .cardName 부분을 .Name 이나 .name 등으로 맞춰주세요!
                if (cardData.uiData != null)
                {
                    return cardData.uiData.wordName; 
                }
                
                return $"카드({id})";
            }

            // 데이터베이스에 등록되지 않은 임시/테스트용 ID 예외 처리
            return $"미확인({id})";
        }


    }
}