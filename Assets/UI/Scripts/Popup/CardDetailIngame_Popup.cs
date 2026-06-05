using UnityEngine;
using System;
using System.Collections.Generic;
using Cards.CardUIDatas;
using Cards.EffectInfos;
using Cards.PlayableCards;
using DG.Tweening;
using Models.CardDatabases;
using TMPro;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace {

    public class CardDetailIngame_Popup : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<CardUIData>, UI_Popup {
        public EUILayer TargetLayer => EUILayer.Popup;

        [Header("UI Elements")] 
        public TextMeshProUGUI txt_DetailCost;
        public TextMeshProUGUI txt_DetailName;
        public TextMeshProUGUI txt_DetailDesc;

        [Header("Animation Targets")]
        public CanvasGroup popupCanvasGroup;
        public RectTransform popupRect;
        public float animDuration = 0.1f;
        
        [Header("Keyword Tooltips")]
        public UI_EffectDetail tooltipPrefab;
        public Transform tooltipContainer;
        private IObjectPool<UI_EffectDetail> tooltipPool;
        private List<UI_EffectDetail> activeTooltips = new List<UI_EffectDetail>();
        
        private void Awake() {
            // 툴팁 오브젝트 풀 초기화
            tooltipPool = new ObjectPool<UI_EffectDetail>(
                () => Instantiate(tooltipPrefab, tooltipContainer),
                (obj) => obj.gameObject.SetActive(true),
                (obj) => obj.gameObject.SetActive(false),
                (obj) => Destroy(obj.gameObject),
                true, 3, 10
            );
        }

        public void ReceiveData(CardUIData data) {
            txt_DetailName.text = data.wordName;
            txt_DetailCost.text = data.cost.ToString();

            txt_DetailDesc.text = data.desc;

            // 1. 기존에 켜져 있던 툴팁 전부 회수
            foreach (var tooltip in activeTooltips) {
                tooltipPool.Release(tooltip);
            }
            activeTooltips.Clear();

            // 2. 카드가 가진 키워드를 기반으로 툴팁 생성
            var keywordList = data.Keywords;
            if (keywordList != null) {
                foreach (CardKeyword keyword in keywordList) {
                    if (KeywordDatabase.TryGetKeywordData(keyword, out string title, out string desc)) {
                        UI_EffectDetail tooltipObj = tooltipPool.Get();
                        tooltipObj.Init(title, desc);
                        activeTooltips.Add(tooltipObj);
                    }
                }
            }
        }

        public void OpenAction() {
            if (popupCanvasGroup == null || popupRect == null) return;

            // 트윈 데이터 초기화
            popupCanvasGroup.DOKill();
            popupRect.DOKill();

            // 1. 애니메이션 시작 전 초기 값 강제 세팅
            popupCanvasGroup.alpha = 0f;
            popupRect.localScale = Vector3.one * 0.8f;

            // 2. 배경은 페이드만, 팝업 본체는 페이드 + 크기 변화 동시 실행
            popupCanvasGroup.DOFade(1f, animDuration);
            popupRect.DOScale(1f, animDuration).SetEase(Ease.OutQuint);
        }

        public void CloseAction(Action onComplete) {
            if (popupCanvasGroup == null || popupRect == null) {
                onComplete?.Invoke();
                return;
            }

            popupCanvasGroup.DOKill();
            popupRect.DOKill();

            // 1. 닫기 애니메이션 역재생
            popupRect.DOScale(0.8f, animDuration).SetEase(Ease.OutQuint);
            
            // 2. 팝업 본체의 페이드아웃이 끝나면 최종적으로 오브젝트를 비활성화하도록 콜백 호출
            popupCanvasGroup.DOFade(0f, animDuration).OnComplete(() => {
                onComplete?.Invoke();
            });
        }
    }
}