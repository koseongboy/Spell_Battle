using Models.PlayerModel;
using Models.SpellPayloads;
using UnityEngine;

namespace Cards.PlayableCards
{
    [CreateAssetMenu(fileName = "Card_Ignite", menuName = "Cards/Fire/Ignite")]
    public class IgniteCard : PlayableCard
    {
        [Header("발화 계수 설정")]
        public int applyStacks = 1;
        public int applyDuration = 2;

        public override void AddToPayload(SpellPayload payload, PlayerModel caster)
        {
            payload.TargetPayload.AddStatus(StatusType.Ignite, applyStacks, applyDuration);
        }
    }
}
