using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Views.LobbyView
{
    public abstract class LobbyView : MonoBehaviour
    {
        [Header("Buttons")] //해당 버튼들은 꼭 필요함
        public Button randomMatchButton;
        public Button createRoomButton;
        public Button joinRoomButton;
        public Button cancelButton; //이미 만든 방에서 나가기 (랜덤매치 중 방 파고 대기하는 상황도 포함)

        public abstract string GetInputCode(); // 방코드 입력받을 인풋 필드 값을 대문자로 변환해서 리턴해주세요

        public abstract void ShowMainMenu(); // 방 정보 패널 닫고 메인 메뉴 패널 열기 (추후 수정 가능 아직 구조 감이 살짝 안잡혀서)
        public abstract void ShowRoomInfo(string code, bool isRandomMatch = false); // 메인 메뉴 패널 닫고 방 정보 패널 열기

        public void UpdateStatus(string message) //로깅용으로 대충 만듦
        {
            Debug.Log(message);
        }
    }
}
