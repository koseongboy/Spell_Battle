using UnityEngine;

namespace ScriptableObjects.ScrollData
{
    [CreateAssetMenu(fileName = "NewScroll", menuName = "Magic/ScrollData")]
    public class ScrollData : ScriptableObject
    {
        public string word;
        public int cost;
    }
}
