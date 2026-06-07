using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    /// <summary>
    /// ConfirmPopup에 전달할 단일/다중 데이터 명세 구조체
    /// </summary>
    public struct ConfirmPopupData
    {
        public string message;       // 중앙에 띄울 텍스트 내용
        public Action onConfirm;     // 확인 버튼 클릭 시 실행할 함수
        public Action onCancel;      // 취소 버튼 클릭 시 실행할 함수 (필요 없으면 null 가능)
    }
    
    public class Confirm_Popup : MonoBehaviour, UI_ILayerInfo, UI_Popup, UI_IDataReceiver<ConfirmPopupData>
    {
        public EUILayer TargetLayer => EUILayer.Top;
        
        [Header("UI 요소 연결")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("애니메이션 대상")]
        [SerializeField] private CanvasGroup bgCanvasGroup;  // 배경 어두운 Dim 패널
        [SerializeField] private RectTransform popupWindowRect; // 중앙 Confirm 창 패널

        [Header("애니메이션 설정")]
        [SerializeField] private float animDuration = 0.1f;     // 연출 시간
        
        // 전달받은 콜백 캐싱용 변수
        private Action onConfirmAction;
        private Action onCancelAction;

        private void Awake()
        {
            // 버튼 컴포넌트에 이벤트 리스너 할당
            confirmButton.onClick.AddListener(OnConfirmClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        /// <summary>
        /// 🛠️ [수정 부분] UILoader의 SendDataToUI 함수가 내부적으로 호출해 줄 데이터 주입 메서드입니다.
        /// 프로젝투 내의 'IInitWithData<T>' 같은 인터페이스 이름을 쓰고 계시다면 해당 인터페이스를 상속받아 구현하세요.
        /// </summary>
        public void ReceiveData(ConfirmPopupData data)
        {
            messageText.text = data.message;
            onConfirmAction = data.onConfirm;
            onCancelAction = data.onCancel;
        }

        private void OnConfirmClicked()
        {
            // 안전하게 확인 액션 호출 후 창 닫기
            onConfirmAction?.Invoke();
            CloseAction(onCancelAction);
        }

        private void OnCancelClicked()
        {
            // 안전하게 취소 액션 호출 후 창 닫기
            onCancelAction?.Invoke();
            CloseAction(onCancelAction);
        }

        public void OpenAction() {
            bgCanvasGroup.DOKill();
            popupWindowRect.DOKill();

            bgCanvasGroup.alpha = 0f;
            popupWindowRect.localScale = Vector3.one * 0.6f;

            bgCanvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuint);
            popupWindowRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
        }

        public void CloseAction(Action onAnimationComplete) {
            bgCanvasGroup.DOKill();
            popupWindowRect.DOKill();

            bgCanvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuad);
            popupWindowRect.DOScale(Vector3.one * 0.6f, animDuration).SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    onAnimationComplete?.Invoke();
                    UILoader.Instance.HideUI("Confirm_Popup");
                });
        }
    }
}
