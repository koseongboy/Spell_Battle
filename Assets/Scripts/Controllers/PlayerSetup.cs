using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Controllers.TurnControllers; // 🌟 턴 컨트롤러에 접근하기 위해 추가

namespace Controllers.PlayerSetup
{
    public class PlayerSetup : NetworkBehaviour
    {
        private Transform mainCam;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                // 회전 세팅
                if (IsServer) transform.rotation = Quaternion.Euler(0, 0, 0); 
                else transform.rotation = Quaternion.Euler(0, 180f, 0); 

                // 카메라 세팅 코루틴 시작
                StartCoroutine(SetupCameraRoutine());
            }
        }

        private IEnumerator SetupCameraRoutine()
        {
            // 1. 배틀 씬의 TurnController가 준비되고, 카메라가 등록될 때까지 안전하게 대기
            while (TurnController.Instance == null || TurnController.Instance.BattleMainCamera == null)
            {
                yield return null;
            }

            // 2. 씬을 뒤지지 않고, TurnController가 쥐고 있는 '진짜' 카메라를 바로 가져옴!
            Camera cam = TurnController.Instance.BattleMainCamera;
            
            mainCam = cam.transform;
            mainCam.SetParent(this.transform);
            mainCam.localPosition = new Vector3(2f, 1.6f, -2f); 
            mainCam.localRotation = Quaternion.Euler(7.5f, -21f, 0f);
            
            Debug.Log($"[PlayerSetup] 🎯 턴 컨트롤러 지정 카메라 세팅 완벽 성공!: {cam.gameObject.name}");
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && mainCam != null)
            {
                mainCam.SetParent(null);
                mainCam.position = new Vector3(0, 10f, -10f); 
                mainCam.rotation = Quaternion.Euler(45f, 0, 0);
            }
        }
    }
}