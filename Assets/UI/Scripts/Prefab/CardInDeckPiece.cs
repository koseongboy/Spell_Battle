using Cards.EffectInfos;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Cards.PlayableCards;

namespace DefaultNamespace
{
    public class CardInDeckPiece : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText; // 예: "2"
        [SerializeField] private Button clickButton;

        public void Init(PlayableCard data, int count, Action<PlayableCard> onRemoveClick)
        {
            costText.text = data.uiData.cost.ToString();
            nameText.text = data.uiData.wordName;
            countText.text = count.ToString();

            clickButton.onClick.RemoveAllListeners();
            // 우측 리스트에서 클릭하면 덱에서 제거하는 로직 연결
            clickButton.onClick.AddListener(() => onRemoveClick?.Invoke(data)); 
        }
    }
}
