using UnityEngine;
using System.Collections.Generic;
using DefaultNamespace;

namespace DefaultNamespace
{
    public class HandLayoutManager : MonoBehaviour
    {
        public static HandLayoutManager Instance { get; private set; }

        [Header("부채꼴 정렬 세팅")]
        public float cardSpacing = 120f;   // 카드 간의 가로 간격
        public float curveHeight = 15f;    // 둥글게 내려가는 Y축 포물선 높이
        public float anglePerCard = 5f;    // 카드 1장당 벌어지는 각도

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // PlayerUI에서 카드가 세팅된 직후 이 함수를 호출해줌
        public void ArrangeCards(List<UI_Card_InHand> activeCards)
        {
            int count = activeCards.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                // 중심을 0으로 맞추기 위한 정규화 (예: 3장이면 -1, 0, 1)
                float normalizedPosition = i - (count - 1) / 2f;

                // 1. 가로 위치 (X)
                float xPos = normalizedPosition * cardSpacing;

                // 2. 세로 곡선 위치 (Y) - 중심에서 멀어질수록 제곱으로 아래로 내려감 (포물선)
                float yPos = -Mathf.Abs(normalizedPosition * normalizedPosition) * curveHeight;

                // 3. 회전 (Z) - 양 끝으로 갈수록 각도가 꺾임
                float zRot = -normalizedPosition * anglePerCard;

                // 카드에게 계산된 위치와 각도를 전달
                activeCards[i].SetLayout(new Vector3(xPos, yPos, 0), Quaternion.Euler(0, 0, zRot));
            }
        }
    }
}