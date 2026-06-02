using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Models.CardDatabases;
using Cards.EffectInfos;
using Controllers.PlayerController;
using TMPro;
using UnityEngine.Pool;

namespace DefaultNamespace
{
    public class Mulligan_FullScreen : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<PlayerController>
    {
        public EUILayer TargetLayer => EUILayer.FullScreen;
        
        [Header("UI 연결")]
        public GameObject MulliganPanel;       // 이 멀리건 화면 전체 객체
        public Transform CardContainer;        // 카드들이 나열될 부모 (Horizontal Layout Group 등)
        public GameObject MulliganCardPrefab;  // 방금 만든 UI_MulliganCard가 붙은 프리팹
       
        [Header("버튼 및 대기 상태 제어")]
        public Button ExchangeButton;          // 하단 "교환" 버튼
        public TextMeshProUGUI WaitingText;     // "상대방을 기다리고 있습니다..."
        
        private PlayerController localPlayer;
        private IObjectPool<UI_Card_Mulligan> _cardPool;
        private List<UI_Card_Mulligan> _activeCards = new List<UI_Card_Mulligan>();

        private void Awake()
        {
            // 오브젝트 풀 초기화
            _cardPool = new ObjectPool<UI_Card_Mulligan>(
                createFunc: () => 
                {
                    GameObject obj = Instantiate(MulliganCardPrefab, CardContainer);
                    return obj.GetComponent<UI_Card_Mulligan>();
                },
                actionOnGet: (card) => card.gameObject.SetActive(true),
                actionOnRelease: (card) => 
                {
                    card.gameObject.SetActive(false);
                    card.transform.SetParent(CardContainer); // 반납 시 부모 원상복구
                },
                actionOnDestroy: (card) => Destroy(card.gameObject),
                collectionCheck: false,
                defaultCapacity: 5,
                maxSize: 10
            );
            
            if (ExchangeButton != null)
            {
                ExchangeButton.onClick.AddListener(OnExchangeButtonClicked);
            }
        }
        
        private void OnDisable()
        {
            ReleaseAllCards();
        }
        
        public void ReceiveData(PlayerController player) {
            Debug.Log("Mulligan_Popup ReceiveData 진입");
            localPlayer = player;
            MulliganPanel.SetActive(true);
            
            ExchangeButton.gameObject.SetActive(true);
            if (WaitingText != null) WaitingText.gameObject.SetActive(false);
            
            ReleaseAllCards();

            // 기존에 컨테이너에 있던 더미 카드들 삭제
            foreach (Transform child in CardContainer)
            {
                Destroy(child.gameObject);
            }

            // 내 초기 손패를 읽어와서 멀리건 카드 UI 생성
            var initialHand = localPlayer.model.Hand.localHand;
            for (int i = 0; i < initialHand.Count; i++)
            {
                int cardId = initialHand[i];
                var rawCardData = CardDatabase.GetCardById(cardId);
                GenericCard genericCard = rawCardData as GenericCard;

                if (genericCard != null)
                {
                    UI_Card_Mulligan cardUI = _cardPool.Get();
                    cardUI.transform.SetAsLastSibling();
                    
                    // Init: 클릭 시 PlayerController의 ToggleMulliganIndex를 호출하도록 람다식 전달
                    cardUI.Init(genericCard, i, (clickedIndex) => 
                    {
                        localPlayer.ToggleMulliganIndex(clickedIndex);
                    });
                    
                    // 활성화된 카드 리스트에 추적 추가
                    _activeCards.Add(cardUI);
                }
            }
        }

        private void OnExchangeButtonClicked()
        {
            if (localPlayer != null)
            {
                localPlayer.SubmitFinalMulligan();
            }
            
            ExchangeButton.gameObject.SetActive(false);
            if (WaitingText != null) {
                WaitingText.gameObject.SetActive(true);
                WaitingText.text = "상대방을 기다리는 중...";
            }
        }
        
        private void ReleaseAllCards()
        {
            foreach (var card in _activeCards)
            {
                _cardPool.Release(card);
            }
            _activeCards.Clear();
        }
    }
}
