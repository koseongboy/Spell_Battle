using UnityEngine;

namespace Cards.EffectInfos
{
    [CreateAssetMenu(fileName = "New Effect Info", menuName = "Cards/1. Effect info(Keyword)")]
    public class EffectInfo : ScriptableObject
    {
        [Header("효과 키워드 정보")]
        public string effectName; // 예: "발화", "강굽이"
        
        [TextArea(2, 4)]
        public string desc; // 예: "대상이 턴이 끝날 때 스택 당 데미지를 받음."
    }
}
