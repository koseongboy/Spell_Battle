using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class DeckListPiece_Room : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txt_DeckName;
        [SerializeField] private TextMeshProUGUI txt_DeckSummary;
        [SerializeField] private TextMeshProUGUI txt_CardCount;
        [SerializeField] private Image img_Element;
        [SerializeField] private Button btn_Click;
        
        public void Setup(DeckMetaData deckData, Action<string> onDeckSelected)
        {
            txt_DeckName.text = deckData.Name;
            txt_CardCount.text = deckData.CardCount;
            
            // TODO : Element에 따라 다른 Icon 표시
            // TODO : Element에 따라 Frame 색 바꿔주기

            if (btn_Click != null) {
                btn_Click.onClick.RemoveAllListeners();
                btn_Click.onClick.AddListener(() => {
                    onDeckSelected?.Invoke(deckData.Id);
                });
            }
        }
    }
}
