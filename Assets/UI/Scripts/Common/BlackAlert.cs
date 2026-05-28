using TMPro;
using UnityEngine;
using UnityEngine.EventSystems; // 클릭 이벤트 처리를 위해 추가
using DG.Tweening; // DOTween 사용을 위해 추가

namespace DefaultNamespace
{
    // CanvasGroup 컴포넌트가 없다면 자동으로 추가되도록 설정
    [RequireComponent(typeof(CanvasGroup))]
    public class BlackAlert : MonoBehaviour, UI_IDataReceiver<string>, UI_ILayerInfo, IPointerClickHandler
    {
        public EUILayer TargetLayer => EUILayer.Top;
        
        [SerializeField] private TextMeshProUGUI message;
        private float moveOffset = 50f;     // 위아래로 움직일 거리
        private float animDuration = 0.2f;  // 애니메이션 재생 시간

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 originalPos;
        private Tween hideTimer;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            
            // Enable되기 전의 초기 위치를 캐싱
            originalPos = rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            // 이전에 실행 중이던 트윈이나 타이머가 있다면 겹치지 않게 취소
            rectTransform.DOKill();
            canvasGroup.DOKill();
            hideTimer?.Kill();

            // 1. 초기 상태 세팅: 약간 아래로 이동, 투명도 0
            rectTransform.anchoredPosition = originalPos + new Vector2(0, -moveOffset);
            canvasGroup.alpha = 0f;

            // 2. 원래 위치로 올라오며 서서히 페이드 인
            rectTransform.DOAnchorPos(originalPos, animDuration).SetEase(Ease.OutQuad);
            canvasGroup.DOFade(1f, animDuration);

            // 3. 3초 뒤에 Hide 메서드 실행 예약
            hideTimer = DOVirtual.DelayedCall(3f, Hide);
        }

        public void ReceiveData(string data) 
        {
            message.text = data;
        }

        // UI가 클릭되었을 때 호출됨 (해당 객체에 Image 등의 Raycast Target이 있어야 작동)
        public void OnPointerClick(PointerEventData eventData)
        {
            Hide();
        }

        private void Hide()
        {
            // 예약된 3초 타이머 취소 (클릭으로 호출됐을 경우를 대비)
            hideTimer?.Kill();
            rectTransform.DOKill();
            canvasGroup.DOKill();

            // 1. 현재 위치에서 위로 올라가며 서서히 페이드 아웃
            Vector2 targetPos = originalPos + new Vector2(0, moveOffset);
            
            rectTransform.DOAnchorPos(targetPos, animDuration).SetEase(Ease.InQuad);
            canvasGroup.DOFade(0f, animDuration).OnComplete(() =>
            {
                // 2. 페이드 아웃이 완전히 끝난 후, 처음 위치로 돌려놓고 객체 비활성화
                rectTransform.anchoredPosition = originalPos;
                gameObject.SetActive(false);
            });
        }
    }
}