using Models.EffectCommands;
using Models.PlayerModels;
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

        public override void ApplyCardEffects(SpellPayload payload, PlayerModel caster, PlayerModel enemy)
        {
            payload.AddCommand(new IgniteCommand(enemy, applyDuration, applyStacks));
        }
    }
}
