using UnityEngine;
using System;
using System.Collections.Generic;
using Cards.EffectInfos;
using Cards.PlayableCards;
using DG.Tweening;
using Models.CardDatabases;
using TMPro;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace {
    public struct CardDetailPayload {
        public PlayableCard CardData;
        public bool CanAdd; // 열람 모드일 경우 false
        public Action<PlayableCard> OnConfirmAdd; // 추가 버튼을 눌렀을 때 실행될 컨트롤러의 함수
    }

    public class CardDetail_Popup : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<CardDetailPayload>, UI_Popup {
        public EUILayer TargetLayer => EUILayer.Popup;

        [Header("UI Elements")] 
        public TextMeshProUGUI txt_DetailCost;
        public TextMeshProUGUI txt_DetailName;
        public TextMeshProUGUI txt_DetailDesc;
        public Image img_DetailIcon;

        public Button btn_DetailAdd;
        public Button btn_Close;
        
        [Header("Animation Targets")]
        public CanvasGroup bgCanvasGroup;
        public CanvasGroup popupCanvasGroup;
        public RectTransform popupRect;
        public float animDuration = 0.1f;
        
        [Header("Keyword Tooltips")]
        public UI_EffectDetail tooltipPrefab;
        public Transform tooltipContainer;
        private IObjectPool<UI_EffectDetail> tooltipPool;
        private List<UI_EffectDetail> activeTooltips = new List<UI_EffectDetail>();
        
        private CardDetailPayload currentPayload;

        private void Awake() {
            if (btn_Close != null) {
                btn_Close.onClick.AddListener(() => { UILoader.Instance.HideUI("CardDetail_Popup"); });
            }

            if (btn_DetailAdd != null) {
                btn_DetailAdd.onClick.AddListener(() => {
                    // 컨트롤러가 넘겨준 '덱에 카드 추가' 함수 실행
                    currentPayload.OnConfirmAdd?.Invoke(currentPayload.CardData);
                    // 완료 후 팝업 닫기
                    UILoader.Instance.HideUI("CardDetail_Popup");
                });
            }
            
            // 툴팁 오브젝트 풀 초기화
            tooltipPool = new ObjectPool<UI_EffectDetail>(
                () => Instantiate(tooltipPrefab, tooltipContainer),
                (obj) => obj.gameObject.SetActive(true),
                (obj) => obj.gameObject.SetActive(false),
                (obj) => Destroy(obj.gameObject),
                true, 3, 10
            );
        }

        public void ReceiveData(CardDetailPayload data) {
            currentPayload = data;

            txt_DetailName.text = data.CardData.Name;
            txt_DetailCost.text = data.CardData.Cost.ToString();

            txt_DetailDesc.text = data.CardData.uiData.desc;
            img_DetailIcon.sprite = data.CardData.uiData.icon;

            // 열람 모드(CanAdd == false)일 경우 아래 '추가' 버튼을 아예 끕니다.
            btn_DetailAdd.gameObject.SetActive(data.CanAdd);
            
            // 1. 기존에 켜져 있던 툴팁 전부 회수
            foreach (var tooltip in activeTooltips) {
                tooltipPool.Release(tooltip);
            }
            activeTooltips.Clear();

            // 2. 카드가 가진 키워드를 기반으로 툴팁 생성
            var keywordList = data.CardData.uiData.Keywords;
            if (keywordList != null) {
                foreach (CardKeyword keyword in keywordList) {
                    if (CardDatabase.Instance.TryGetKeywordData(keyword, out string title, out string desc)) {
                        UI_EffectDetail tooltipObj = tooltipPool.Get();
                        tooltipObj.Init(title, desc);
                        activeTooltips.Add(tooltipObj);
                    }
                }
            }
        }

        public void OpenAction() {
            if (bgCanvasGroup == null || popupCanvasGroup == null || popupRect == null) return;

            // 트윈 데이터 초기화
            bgCanvasGroup.DOKill();
            popupCanvasGroup.DOKill();
            popupRect.DOKill();

            // 1. 애니메이션 시작 전 초기 값 강제 세팅
            bgCanvasGroup.alpha = 0f;
            popupCanvasGroup.alpha = 0f;
            popupRect.localScale = Vector3.one * 0.8f;

            // 2. 배경은 페이드만, 팝업 본체는 페이드 + 크기 변화 동시 실행
            bgCanvasGroup.DOFade(1f, animDuration);
            popupCanvasGroup.DOFade(1f, animDuration);
            popupRect.DOScale(1f, animDuration).SetEase(Ease.OutQuint);
        }

        public void CloseAction(Action onComplete) {
            if (bgCanvasGroup == null || popupCanvasGroup == null || popupRect == null) {
                onComplete?.Invoke();
                return;
            }

            bgCanvasGroup.DOKill();
            popupCanvasGroup.DOKill();
            popupRect.DOKill();

            // 1. 닫기 애니메이션 역재생
            bgCanvasGroup.DOFade(0f, animDuration);
            popupRect.DOScale(0.8f, animDuration).SetEase(Ease.OutQuint);
            
            // 2. 팝업 본체의 페이드아웃이 끝나면 최종적으로 오브젝트를 비활성화하도록 콜백 호출
            popupCanvasGroup.DOFade(0f, animDuration).OnComplete(() => {
                onComplete?.Invoke();
            });
        }
    }
}