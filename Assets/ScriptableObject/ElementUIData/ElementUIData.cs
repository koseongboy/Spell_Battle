using Cards.CardUIDatas;
using UnityEngine;

namespace DefaultNamespace
{
    [CreateAssetMenu(fileName = "NewElementUIData", menuName = "CardData/ElementUIData")]
    public class ElementUIData : ScriptableObject
    {
        public Property Property;
        public string Name;
        public Sprite Icon;
        public Color Color;
    }
}
