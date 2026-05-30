using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class CostInDeckEdit : MonoBehaviour
    {
        public int cost; // 인스펙터에서 0, 1, 2... 10 지정 (10은 10+ 카드를 의미)
        public Button button;
        public GameObject highlightObj;

        private void Reset()
        {
            button = GetComponent<Button>();
        }
    }
}
