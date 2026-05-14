using Models.PlayerModels;
using UnityEngine;

namespace Models.EffectCommands
{
    public class IgniteCommand : EffectCommand
    {
        int duration, stacks;
        public IgniteCommand(PlayerModel target, int duration, int stacks) : base(target)
        {
            this.duration = duration;
            this.stacks = stacks;
        }

        public override void Execute(float multiplier = 1)
        {
            target.AddStatus(StatusType.Ignite, stacks, duration);
        }
    }
}
