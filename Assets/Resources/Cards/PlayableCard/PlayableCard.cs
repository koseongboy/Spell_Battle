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
        public virtual void AddToPayload(SpellPayload payload, PlayerModel caster, PlayerModel enemy)
        {
            // GenericCard에서 구현하겠음.
            // 다른 곳에서 Card에 대한 걸 다루려다가, 문제가 생기길래 옮겨버림.
            
            // if (uiData == null)
            // {
            //     Debug.LogError($"[PlayableCard 에러] {this.name} 카드의 uiData가 인스펙터에 연결되지 않았습니다!");
            //     return;
            // }
            // payload.AddWord(Name);
            // payload.AddProperty(Prop);
            // payload.EnqueuePendingCard(this); // 카드 등록만 수행
        }

        public abstract void ApplyCardEffects(SpellPayload payload, PlayerModel caster, PlayerModel enemy);
    }
}
