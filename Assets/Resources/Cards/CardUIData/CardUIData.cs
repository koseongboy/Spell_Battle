using UnityEngine;
using Cards.EffectInfos;
using System.Collections.Generic;



namespace Cards.CardUIDatas
{
    public enum Property
    {
        None, //이거에 대해선 아무런 UI적 조치 하지 말 것. (혹은 설정 안한 오류임을 티를 마구 내면 더 좋음)
        Attack,
        Deffence,
        Chain,
        Fire,
        Water,
        Ground,
        Thunder,
        Wind,
        Ice,
        Vacuity,
        Vision,
        Life,
        Void
    }
    [System.Serializable]
    public class CardUIData
    {
        [Header("기본 식별 정보")]
        public int id;             // 예: 31001
        public string wordName;    // 예: "발화" (카드에 적힐 이름)
        public int cost;           // 예: 1
        public Property property;  // 속성

        [Header("효과 및 설명")]
        public List<EffectInfo> Effects; // 위에서 만든 'EffectInfo'를 끌어다 연결

        [TextArea(2, 4)]
        public string desc; // 예: "대상에게 2턴 동안 발화 중첩을 1 적용한다."
        
        // (todo) 카드 일러스트 주소?
    }
}
