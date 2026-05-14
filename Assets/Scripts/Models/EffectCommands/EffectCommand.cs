using UnityEngine;
using Models.PlayerModels;
using Cards.PlayableCards;

namespace Models.EffectCommands
{
    public abstract class EffectCommand
    {
        protected PlayerModel target;

        public EffectCommand(PlayerModel target)
        {
            this.target = target;
        }
        public abstract void Execute(float multiplier = 1.0f); //multiplier는 llm 평가 받고 곱해지는 계수 자료형은 미정
    }
}
