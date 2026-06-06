using Cards.PlayableCards;
using UnityEngine;

namespace DefaultNamespace
{
    [CreateAssetMenu(fileName = "NewKeywordData", menuName = "CardData/KeywordData")]
    public class KeywordData : ScriptableObject
    {
        public CardKeyword Keyword;
        public string Title;
        [TextArea] public string Description;
    }
}
