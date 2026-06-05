using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;
using DefaultNamespace;

public class Loading_Common : MonoBehaviour, UI_ILayerInfo
{
    public EUILayer TargetLayer => EUILayer.Top;
    
    
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private RectTransform loadingImageRect;
    
    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.2f; // 페이드 인에 걸리는 시간
    [SerializeField] private float pulseDuration = 0.6f; 
    [SerializeField] private float maxScale = 1.2f;
    [SerializeField] private float minScale = 0.8f;
    
    private void Start()
    {
        // 1. 은은하게 나타나는 페이드 인 효과
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, fadeDuration);

        // 2. "Loading..." 텍스트 애니메이션 시작
        StartCoroutine(AnimateTextCoroutine());
        
        StartPulseAnimation();
    }
    
    private void OnDestroy()
    {
        if (loadingImageRect != null) loadingImageRect.DOKill();
    }
    
    private void StartPulseAnimation()
    {
        if (loadingImageRect == null) return;

        // 중복 트윈 방지를 위해 기존 트윈 제거
        loadingImageRect.DOKill();

        // 시작 크기를 최소 크기로 설정
        loadingImageRect.localScale = Vector3.one * minScale;

        // 최소 크기에서 최대 크기로 커지는 트윈 실행
        // 팩트: LoopType.Yoyo를 주어야 커졌다 작아지는 왕복 연출이 완성이 되며, InOutSine으로 정점 감속을 줍니다.
        loadingImageRect.DOScale(Vector3.one * maxScale, pulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private IEnumerator AnimateTextCoroutine()
    {
        // 1초 대기 객체를 캐싱하여 가비지 컬렉션(GC) 최적화
        WaitForSeconds waitTime = new WaitForSeconds(0.2f);
        int dotCount = 1;

        while (true)
        {
            // 점 개수에 맞게 문자열 생성 (예: dotCount가 3이면 "...")
            string dots = new string('.', dotCount);
            loadingText.text = $"Loading{dots}";

            dotCount++;
            if (dotCount > 3)
            {
                dotCount = 1; // 3개를 초과하면 다시 1개로 초기화
            }

            // 1초 대기 후 다음 루프 실행
            yield return waitTime;
        }
    }
}