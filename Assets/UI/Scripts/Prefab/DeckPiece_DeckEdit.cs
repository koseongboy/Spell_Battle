using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Cards.CardUIDatas;
using Models.CardDatabases;

namespace DefaultNamespace
{
    public class DeckPiece_DeckEdit : MonoBehaviour
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
            var elementData = CardDatabase.Instance.GetElementData(repProp);
            if (elementData != null) {
                img_ElementIcon.sprite = elementData.Icon;
            }

            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(() => onClick?.Invoke(deckName));
        }

        public void SetSelected(bool isSelected) {
            highlightImage.enabled = isSelected;
        }
    }
}
