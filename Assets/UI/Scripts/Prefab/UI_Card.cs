using System;
using Cards.EffectInfos;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class UI_Card : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private Button clickButton;

        private GenericCard cardData;
        private Action<GenericCard> onClickAction;

        public void Init(GenericCard data, Action<GenericCard> onClick)
        {
            cardData = data;
            onClickAction = onClick;

            nameText.text = data.uiData.wordName;
            costText.text = data.uiData.cost.ToString();
            descText.text = data.uiData.desc;

            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(() => onClickAction?.Invoke(cardData));
        }
    }
}
