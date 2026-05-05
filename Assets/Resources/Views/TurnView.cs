using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Models.TurnModel;

namespace Views.TurnView
{
    public abstract class TurnView : MonoBehaviour
    {
        public abstract void UpdateUI(GamePhase phase, bool isMyTurn); //턴에 대한 진행 상황
        public void LogMessage(string message)
        {
            Debug.Log($"[View] {message}");
            // 나중에 화면에 로그 텍스트를 띄우고 싶다면 여기에 구현
        }
    }
}
