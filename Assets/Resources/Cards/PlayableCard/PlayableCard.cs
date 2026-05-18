using UnityEngine;
using Cards.CardUIDatas;
using Models.PlayerModels;
using Models.SpellPayloads;

namespace Cards.PlayableCards
{
    public abstract class PlayableCard : ScriptableObject
    {
        [Header("카드 데이터 연결")]
        public CardUIData uiData; 

        // 카드의 고유 ID를 쉽게 꺼내기 위한 헬퍼 프로퍼티
        public int Id => uiData.id;
        public string Name => uiData.wordName;
        public int Cost => uiData.cost;
        public Property Prop => uiData.property;

        // 실제 발동 로직 (자식 클래스들이 반드시 구현해야 함)
        public void AddToPayload(SpellPayload payload, PlayerModel caster, PlayerModel enemy)
        {
            payload.AddWord(uiData.wordName);
            payload.AddProperty(Prop);
            ApplyCardEffects(payload, caster, enemy);
        }

        public abstract void ApplyCardEffects(SpellPayload payload, PlayerModel caster, PlayerModel enemy);
    }
}
