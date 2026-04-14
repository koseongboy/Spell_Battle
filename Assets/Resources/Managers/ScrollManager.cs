using UnityEngine;
using System.Collections.Generic;
using Controllers.ScrollController;
using ScriptableObjects.ScrollData;

namespace Managers.ScrollManager
{
    public class ScrollManager : MonoBehaviour
    {
        [Header("설정")]
        public GameObject scrollPrefab;       // 3D 스크롤 프리팹
        public Transform playerTransform;     // 플레이어 (기준점)
        public Transform blackHole;           // 스크롤이 튀어나올 블랙홀 위치
        
        [Header("부채꼴 정렬 세팅")]
        public float radius = 8.0f;           // 플레이어로부터 떨어질 거리
        public float arcAngle = 120f;          // 부채꼴이 벌어질 최대 각도

        // 현재 손에 들고 있는 스크롤 목록
        private List<ScrollController> hand = new List<ScrollController>(); 

        void Update()
        {
            // 테스트용: D 키를 누르면 스크롤을 1장 뽑습니다.
            if (Input.GetKeyDown(KeyCode.D))
            {
                DrawOneScroll();
            }
        }

        public void DrawOneScroll()
        {
            // 1. 블랙홀 위치에서 스크롤 프리팹 생성
            GameObject newScrollObj = Instantiate(scrollPrefab, blackHole.position, blackHole.rotation);
            ScrollController scroll = newScrollObj.GetComponent<ScrollController>();

            // (임시 데이터 주입 - 나중에는 덱에서 가져옵니다)
            ScrollData dummyData = ScriptableObject.CreateInstance<ScrollData>();
            dummyData.word = "마법_" + (hand.Count + 1);
            scroll.Setup(dummyData);

            // 2. 손 패 목록에 추가
            hand.Add(scroll);

            // 3. 손에 있는 모든 스크롤 자리 재배치
            UpdateHandPositions();
        }

        private void UpdateHandPositions()
        {
            int count = hand.Count;

            for (int i = 0; i < count; i++)
            {
                // 스크롤 개수에 따라 각도 분배 (-각도 ~ +각도)
                float angle = 0;
                if (count > 1)
                {
                    angle = Mathf.Lerp(-arcAngle / 2f, arcAngle / 2f, (float)i / (count - 1));
                }

                // 1. 목표 위치 계산 (플레이어 앞쪽 + 각도 회전 + 거리 + 높이)
                Vector3 direction = Quaternion.Euler(0, angle, 0) * playerTransform.forward;
                Vector3 targetPos = playerTransform.position + (direction * radius) + new Vector3(0, 1.5f, 0);

                // 2. 목표 회전 계산 (카메라를 바라보도록)
                // 스크롤 앞면이 보이기 위해 카메라 반대 방향으로 뒤집습니다.
                Vector3 lookDirection = targetPos - Camera.main.transform.position;
                Quaternion targetRot = Quaternion.LookRotation(lookDirection + new Vector3(0, -90, 0));

                // 3. 스크롤에게 날아갈 목표 전달
                hand[i].FlyTo(targetPos, targetRot);
            }
        }
    }
}
