using System.Collections;
using Models.TurnModel;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace DefaultNamespace
{
    public class UpperTurnUI : MonoBehaviour
    {
        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI turnText;
        // [SerializeField] private Image backgroundImage; // (선택) 배경색도 바꾸고 싶다면 주석 해제

        [Header("색상 세팅")]
        public Color myTurnColor = new Color(0.2f, 0.6f, 1f); // 파란색
        public Color enemyTurnColor = new Color(1f, 0.3f, 0.3f); // 빨간색


        public void SetTurnState(bool isMyTurn)
        {
            if (isMyTurn)
            {
                turnText.text = "내 턴";
                turnText.color = myTurnColor;
            }
            else
            {
                turnText.text = "상대 턴";
                turnText.color = enemyTurnColor;
            }
        }
    }
}
