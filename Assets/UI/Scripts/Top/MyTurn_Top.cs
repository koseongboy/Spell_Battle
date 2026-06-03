using System;
using UnityEngine;
using DG.Tweening; // DOTween 네임스페이스 필수

namespace DefaultNamespace
{
    public class MyTurn_Top : MonoBehaviour, UI_ILayerInfo
    {
        public EUILayer TargetLayer => EUILayer.Top;

        [Header("UI 레퍼런스")]
        [Tooltip("전체를 덮는 어두운 배경 이미지 (CanvasGroup 컴포넌트 필요)")]
        [SerializeField] private CanvasGroup bgCanvasGroup; 
        
        [Tooltip("빠르게 지나갈 중앙 텍스트/이미지 오브젝트")]
        [SerializeField] private RectTransform centerObject; 

        [Header("애니메이션 설정")]
        [SerializeField] private float fadeDuration = 0.3f; // 페이드 인/아웃 걸리는 시간
        [SerializeField] private float slideDuration = 0.4f; // 슬라이드 걸리는 시간
        [SerializeField] private float waitTime = 1.0f; // 화면 중앙에서 대기하는 시간
        [SerializeField] private float slideOffset = 1500f; // 화면 밖으로 나가는 X 좌표 거리 (해상도에 맞게 조절)

        private Sequence _animSequence;

        private void OnEnable() 
        {
            // 1. 연속으로 켜질 경우를 대비해 기존 트윈 강제 종료 (버그 방지)
            _animSequence?.Kill();

            // 2. 초기 상태 셋업 (배경 투명, 오브젝트는 화면 오른쪽 밖으로)
            bgCanvasGroup.alpha = 0f;
            centerObject.anchoredPosition = new Vector2(slideOffset, centerObject.anchoredPosition.y);

            // 3. DOTween 시퀀스 생성
            _animSequence = DOTween.Sequence();

            // 4. [페이즈 1: 등장] 배경 페이드인(알파값 0.8) & 중앙 오브젝트 슬라이드인(X: 0)
            // Join()을 사용하면 앞의 애니메이션과 동시에 실행됩니다.
            _animSequence.Append(bgCanvasGroup.DOFade(0.8f, fadeDuration))
                         .Join(centerObject.DOAnchorPosX(0f, slideDuration).SetEase(Ease.OutBack)); // OutBack: 도착할 때 살짝 튕기는 찰진 이징 연출

            // 5. [페이즈 2: 대기] 지정된 시간(1초) 멈춤
            _animSequence.AppendInterval(waitTime);

            // 6. [페이즈 3: 퇴장] 배경 페이드아웃(알파값 0) & 중앙 오브젝트 왼쪽 화면 밖으로 슬라이드아웃
            _animSequence.Append(bgCanvasGroup.DOFade(0f, fadeDuration))
                         .Join(centerObject.DOAnchorPosX(-slideOffset, slideDuration).SetEase(Ease.InBack)); // InBack: 출발할 때 뒤로 살짝 당겼다 나가는 연출

            // 7. [페이즈 4: 종료] 모든 연출이 끝나면 비활성화
            _animSequence.OnComplete(() => {
                UILoader.Instance.HideUI("MyTurn_Top");
            });
        }

        private void OnDisable()
        {
            // 게임 오브젝트가 외부 요인에 의해 강제로 꺼졌을 때, DOTween이 메모리에 남아 에러를 뱉는 것을 방지
            _animSequence?.Kill();
        }
    }
}