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

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.2f; // 페이드 인에 걸리는 시간
    
    private void Start()
    {
        // 1. 은은하게 나타나는 페이드 인 효과
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, fadeDuration);

        // 2. "Loading..." 텍스트 애니메이션 시작
        StartCoroutine(AnimateTextCoroutine());
    }

    private IEnumerator AnimateTextCoroutine()
    {
        // 1초 대기 객체를 캐싱하여 가비지 컬렉션(GC) 최적화
        WaitForSeconds waitTime = new WaitForSeconds(1f);
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