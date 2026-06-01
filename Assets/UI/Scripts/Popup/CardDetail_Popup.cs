using UnityEngine;
using System;
using Cards.EffectInfos;
using DG.Tweening;
using TMPro;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DefaultNamespace {
    public struct CardDetailPayload {
        public GenericCard CardData;
        public bool CanAdd; // 열람 모드일 경우 false
        public Action<GenericCard> OnConfirmAdd; // 추가 버튼을 눌렀을 때 실행될 컨트롤러의 함수
    }

    public class CardDetail_Popup : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<CardDetailPayload>, UI_Popup {
        public EUILayer TargetLayer => EUILayer.Popup;

        [Header("UI Elements")] 
        public TextMeshProUGUI txt_DetailCost;
        public TextMeshProUGUI txt_DetailName;
        public TextMeshProUGUI txt_DetailDesc;

        public Button btn_DetailAdd;
        public Button btn_Close;
        
        [Header("Animation Targets")]
        public CanvasGroup bgCanvasGroup;
        public CanvasGroup popupCanvasGroup;
        public RectTransform popupRect;
        public float animDuration = 0.1f;

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
        }

        public void ReceiveData(CardDetailPayload data) {
            currentPayload = data;

            txt_DetailName.text = data.CardData.Name;
            txt_DetailCost.text = data.CardData.Cost.ToString();

            // TODO: 추후 구현하실 디테일 설명/용어 처리
            txt_DetailDesc.text = data.CardData.uiData.desc;

            // 열람 모드(CanAdd == false)일 경우 아래 '추가' 버튼을 아예 끕니다.
            btn_DetailAdd.gameObject.SetActive(data.CanAdd);
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