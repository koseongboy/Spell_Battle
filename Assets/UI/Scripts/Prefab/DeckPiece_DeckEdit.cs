using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace DefaultNamespace
{
    public class DeckListPiece : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI deckNameText;
        [SerializeField] private Image highlightImage; // 활성화 시 켜질 이미지
        [SerializeField] private Button clickButton;

        public void Init(string deckName, bool isSelected, Action<string> onClick)
        {
            deckNameText.text = deckName;
            highlightImage.enabled = isSelected;

            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(() => onClick?.Invoke(deckName));
        }
    }
}
