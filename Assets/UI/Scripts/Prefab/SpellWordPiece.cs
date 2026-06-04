using Cards.EffectInfos;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class SpellWordPiece : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI txt_Word;
        [SerializeField] Image img_BG;

        public void UpdateUI( GenericCard cardData ) {
            var word = cardData.uiData.wordName;
            var element = cardData.uiData.property;
            
            txt_Word.text = word;
            // TODO : 카드 색 바꾸기
        }
    }
}
