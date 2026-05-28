using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class DeckListPiece_Room : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txt_DeckName;
        [SerializeField] private TextMeshProUGUI txt_CardCount;
        [SerializeField] private Image img_Element;
        
        public void Setup(DeckMetaData deckData)
        {
            txt_DeckName.text = deckData.Name;
            txt_CardCount.text = deckData.CardCount;
            
            // TODO : Element에 따라 다른 Icon 표시
            // TODO : Element에 따라 Frame 색 바꿔주기

            // TODO : UI Piece 클릭 시, 현재 선택한 Deck을 최신화하는 로직
        }
    }
}
