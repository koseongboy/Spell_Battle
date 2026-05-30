using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Models.TurnModel;

namespace Views.TurnView
{
    public class TurnView : MonoBehaviour
    {
        public void UpdateUI(GamePhase phase, bool isMyTurn)
        {
            Debug.Log($"페이즈 변경! 페이즈는: {phase}, 내 턴인가? {isMyTurn}");
        }
        public void LogMessage(string message)
        {
            Debug.Log($"[View] {message}");
            // 나중에 화면에 로그 텍스트를 띄우고 싶다면 여기에 구현
        }
    }
}
