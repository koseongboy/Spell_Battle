using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Models.TurnModel;

namespace Views.TurnView
{
    public class TurnView : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_Text turnInfoText;    // "나의 턴!" or "상대방 턴"
        public TMP_Text phaseText;       // "현재 페이즈: Draw"
        public Button actionButton;      // 페이즈 넘기기 (or 영창 시작) 버튼

        // Controller가 이 함수를 호출해서 화면을 갱신함
        public void UpdateUI(GamePhase phase, bool isMyTurn)
        {
            turnInfoText.text = isMyTurn ? "<color=green>나의 턴!</color>" : "<color=red>상대방 턴 대기중...</color>";
            phaseText.text = $"현재 상태: {phase} Phase";
            
            // 내 턴일 때만 버튼을 누를 수 있게 활성화
            actionButton.interactable = isMyTurn; 
        }

        public void LogMessage(string message)
        {
            Debug.Log($"[View] {message}");
            // 나중에 화면에 로그 텍스트를 띄우고 싶다면 여기에 구현
        }
    }
}
