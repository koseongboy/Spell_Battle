using Models.PlayerModels;
using UnityEngine;

namespace Models.EffectCommands
{
    
    public class HealCommand : EffectCommand
    {
        private int baseHeal;

        public HealCommand(PlayerModel target, int heal) : base(target)
        {
            baseHeal = heal;
        }

        public override void Execute(float multiplier = 1.0f)
        {
            int finalHeal = baseHeal; //(todo) 계산식~
            target.Heal(finalHeal);
        }
    }
}
