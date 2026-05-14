using Models.PlayerModels;
using UnityEngine;

namespace Models.EffectCommands
{
    public class ShieldCommand : EffectCommand
    {
        private int baseShield;
        public ShieldCommand(PlayerModel target, int amount) : base(target)
        {
            baseShield = amount;
        }

        public override void Execute(float multiplier = 1)
        {
            int finalShield = baseShield; // (todo) 계산식
            target.AddShield(finalShield);
        }
    }
}
