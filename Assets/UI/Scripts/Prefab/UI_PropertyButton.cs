using Cards.CardUIDatas;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class UI_PropertyButton : MonoBehaviour
    {
        public Property property; // 인스펙터에서 이 버튼의 속성 하나만 띡 고르면 됨
        public Button button;
        public GameObject highlightObj;

        // 💡 꿀팁: 컴포넌트를 붙이는 순간 자동으로 내부에 있는 Button을 찾아 연결해줍니다.
        private void Reset()
        {
            button = GetComponent<Button>();
        }
    }
}
