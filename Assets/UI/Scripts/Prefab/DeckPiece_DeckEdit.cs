using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Cards.CardUIDatas;

namespace DefaultNamespace
{
    public class DeckListPiece : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI deckNameText;
        [SerializeField] private TextMeshProUGUI deckSummaryText;
        [SerializeField] private Image img_ElementIcon;
        [SerializeField] private Image highlightImage; // 활성화 시 켜질 이미지
        [SerializeField] private Button clickButton;

        public void Init(string deckName, string deckSummary, Property repProp, bool isSelected, Action<string> onClick)
        {
            deckNameText.text = deckName;
            deckSummaryText.text = deckSummary;
            highlightImage.enabled = isSelected;
            if (img_ElementIcon != null) {
                // img_ElementIcon.sprite = GetSpriteByProperty(repProp); // TODO : 속성에 맞는 스프라이트 반환 함수 연결
            }

            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(() => onClick?.Invoke(deckName));
        }
    }
}
