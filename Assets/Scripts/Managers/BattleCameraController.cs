using UnityEngine;
using DG.Tweening; // DOTween 필수

namespace Managers.Cameras
{
    public class BattleCameraController : MonoBehaviour
    {
        public static BattleCameraController Instance { get; private set; }

        private Camera mainCam;
        private Vector3 originalLocalPos;
        private Quaternion originalLocalRot;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // PlayerSetup에서 카메라 세팅이 끝난 직후에 호출하여 '원래 자리'를 기억합니다.
        public void InitCamera(Camera cam)
        {
            mainCam = cam;
            originalLocalPos = mainCam.transform.localPosition;
            originalLocalRot = mainCam.transform.localRotation;
        }

        // 타겟을 향해 카메라를 부드럽게 이동/회전 시킵니다.
        public void FocusOnTarget(Transform target)
        {
            if (mainCam == null || target == null) return;

            // 🌟 이전에 실행 중이던 카메라 애니메이션이 있다면 강제 종료 (충돌 방지)
            mainCam.transform.DOKill();

            // 1. 타겟의 가슴/명치 높이(Vector3.up * 1.5f)를 바라보도록 회전 (0.3초)
            Vector3 lookPosition = target.position + Vector3.up * 1.5f;
            mainCam.transform.DOLookAt(lookPosition, 0.3f).SetEase(Ease.OutCubic);

            // 2. 살짝 앞으로 줌인 (기존 위치에서 카메라가 바라보는 앞 방향으로 2.5만큼 전진)
            Vector3 zoomPos = originalLocalPos + mainCam.transform.forward * 2.5f;
            mainCam.transform.DOLocalMove(zoomPos, 0.3f).SetEase(Ease.OutCubic);
        }

        // 연출이 끝나면 원래 자리로 복귀합니다.
        public void ResetCamera()
        {
            if (mainCam == null) return;

            mainCam.transform.DOKill();

            // 기억해둔 원래 로컬 좌표와 각도로 복귀 (0.3초)
            mainCam.transform.DOLocalMove(originalLocalPos, 0.3f).SetEase(Ease.InOutSine);
            mainCam.transform.DOLocalRotateQuaternion(originalLocalRot, 0.3f).SetEase(Ease.InOutSine);
        }
    }
}