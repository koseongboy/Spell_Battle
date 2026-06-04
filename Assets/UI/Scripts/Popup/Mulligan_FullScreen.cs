using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using UnityEngine.UI;
using Models.CardDatabases;
using Cards.EffectInfos;
using Cards.PlayableCards;
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
        
        private bool isSubmitted = false;

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
            if (localPlayer != null && localPlayer.model.Hand != null)
            {
                localPlayer.model.Hand.localHand.CollectionChanged -= OnHandChanged;
            }
            ReleaseAllCards();
        }
        
        public void ReceiveData(PlayerController player) {
            localPlayer = player;
            isSubmitted = false;
            
            MulliganPanel.SetActive(true);
            ExchangeButton.gameObject.SetActive(true);
            if (WaitingText != null) WaitingText.gameObject.SetActive(false);
            
            ReleaseAllCards();

            // 손패 변화 구독 (서버가 카드를 바꿔주면 감지함)
            if (localPlayer.model.Hand != null)
            {
                localPlayer.model.Hand.localHand.CollectionChanged -= OnHandChanged; // 중복 방지
                localPlayer.model.Hand.localHand.CollectionChanged += OnHandChanged;
            }

            // 화면에 카드 그리기
            RefreshCards();
        }

        private void OnExchangeButtonClicked()
        {
            if (localPlayer != null)
            {
                isSubmitted = true;
                localPlayer.SubmitFinalMulligan();
            }
            
            ExchangeButton.gameObject.SetActive(false);
            if (WaitingText != null) {
                WaitingText.gameObject.SetActive(true);
                WaitingText.text = "상대방을 기다리는 중...";
            }
        }
        
        // 서버에서 내 손패를 변경할 때마다 자동으로 실행되는 함수
        private void OnHandChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshCards();
        }
        
        // 카드 생성 로직을 별도 함수로 분리
        private void RefreshCards()
        {
            ReleaseAllCards();

            var initialHand = localPlayer.model.Hand.localHand;
            for (int i = 0; i < initialHand.Count; i++)
            {
                int cardId = initialHand[i];
                var rawCardData = CardDatabase.GetCardById(cardId);
                PlayableCard card = rawCardData as PlayableCard;

                if (card != null)
                {
                    UI_Card_Mulligan cardUI = _cardPool.Get();
                    cardUI.transform.SetAsLastSibling();

                    cardUI.Init(card, i, (clickedIndex) => 
                    {
                        // 제출 전(isSubmitted == false)일 때만 카드 선택/취소 가능!
                        if (!isSubmitted)
                        {
                            localPlayer.ToggleMulliganIndex(clickedIndex);
                        }
                    });

                    _activeCards.Add(cardUI);
                }
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
