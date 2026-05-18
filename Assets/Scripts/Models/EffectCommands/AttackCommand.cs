using UnityEngine;
using Models.PlayerModels;

namespace Models.EffectCommands
{
    public class AttackCommand : EffectCommand
    {
        private int baseDamage;

        public AttackCommand(PlayerModel target, int damage) : base(target)
        {
            baseDamage = damage;
        }
        public override void Execute(float multiplier = 1.0f)
        {
            int finalDamage = baseDamage; // (todo) 데미지 계산식 적용
            target.TakeDamage(finalDamage);
        }
    }
}
