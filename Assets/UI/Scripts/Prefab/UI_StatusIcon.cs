using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DefaultNamespace
{
    public class UI_StatusIcon : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        public Image IconImage;
        public TextMeshProUGUI StackText;

        // UI 매니저가 이 아이콘을 생성할 때 데이터를 주입하는 함수
        public void Setup(Sprite sprite, int totalStacks)
        {
            if (IconImage != null && sprite != null)
            {
                IconImage.sprite = sprite;
            }

            if (StackText != null)
            {
                // 스택이 1 이상일 때만 표시하거나, 항상 표시하도록 설정 가능
                StackText.text = totalStacks.ToString();
            }
        }
    }
}
