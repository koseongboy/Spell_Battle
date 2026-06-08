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
            mainCam.transform.DOKill();

            // 1. 카메라 위치 계산 (기존과 동일)
            Vector3 targetCamPos = target.position + target.forward * 2.5f + target.right * 0.8f + Vector3.up * 1.5f;
            
            // 2. 고정 지점 (타겟의 가슴)
            Vector3 lookPosition = target.position + Vector3.up * 1.2f;

            // 3. 이동
            mainCam.transform.DOMove(targetCamPos, 0.3f).SetEase(Ease.OutCubic);

            // 🌟 핵심: DOLookAt 대신 Quaternion.LookRotation으로 계산한 방향을 향해 회전
            Vector3 direction = (lookPosition - targetCamPos).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            mainCam.transform.DORotateQuaternion(targetRotation, 0.3f).SetEase(Ease.OutCubic);
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