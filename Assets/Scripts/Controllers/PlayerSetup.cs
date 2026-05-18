using UnityEngine;
using Unity.Netcode;

namespace Controllers.PlayerSetup
{
    public class PlayerSetup : NetworkBehaviour
    {
        private Transform mainCam;
        // Start() 대신 사용하는 NGO 전용 함수입니다. (캐릭터가 네트워크에 소환 완료된 직후 실행됨)
        public override void OnNetworkSpawn()
        {
            // 1. [스폰 위치 지정] - 위치는 오직 '서버(방장)'만이 바꿀 권한이 있습니다.
            if (IsServer)
            {
                // OwnerClientId는 유저의 고유 번호입니다. 방장은 항상 0번, 손님은 1번입니다.
                if (OwnerClientId == 0) 
                {
                    // 방장은 맵의 남쪽(-8)에서 북쪽을 바라보게 세팅
                    transform.position = new Vector3(0, 0, -8f); 
                    transform.rotation = Quaternion.Euler(0, 0, 0); 
                }
                else if (OwnerClientId == 1) 
                {
                    // 접속자는 맵의 북쪽(+8)에서 남쪽(방장 쪽)을 마주보도록 180도 뒤돌아서 세팅
                    transform.position = new Vector3(0, 0, 8f); 
                    transform.rotation = Quaternion.Euler(0, 180f, 0); 
                }
            }

            // 2. [카메라 세팅] - 카메라는 상대방과 공유할 필요 없이 내 화면에서만(IsOwner) 처리합니다.
            if (IsOwner)
            {
                // 맵에 덩그러니 있는 'Main Camera'를 찾습니다.
                mainCam = Camera.main.transform;
                
                // 그 카메라를 '나(마법사)'의 등 뒤에 자식으로 찰싹 붙입니다.
                mainCam.SetParent(this.transform);
                
                // 숄더뷰 느낌으로 위치와 각도를 예쁘게 잡아줍니다.
                mainCam.localPosition = new Vector3(0, 2.5f, -3f); 
                mainCam.localRotation = Quaternion.Euler(15f, 0, 0);
            }


            
        }

        // ✨ [추가됨] 네트워크에서 캐릭터가 삭제(Despawn)되기 직전에 자동으로 실행되는 함수
        public override void OnNetworkDespawn()
        {
            // 내 캐릭터이고, 카메라를 무사히 들고 있었다면
            if (IsOwner && mainCam != null)
            {
                // 1. 카메라를 플레이어 등에서 떼어내어 부모를 없앱니다 (맵 최상단으로 독립)
                mainCam.SetParent(null);

                // 2. (선택 사항) 카메라가 버려진 자리에 덩그러니 있지 않도록, 
                // 다시 메인 메뉴(로비)를 비추기 좋은 기본 위치로 되돌려 놓습니다.
                mainCam.position = new Vector3(0, 10f, -10f); // 예시: 맵 전체를 내려다보는 위치
                mainCam.rotation = Quaternion.Euler(45f, 0, 0);
            }
        }
    }
}
