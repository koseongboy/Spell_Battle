using System;
using Cards.EffectInfos;
using UnityEngine;
using Models.PlayerModels;
using Cards.PlayableCards;
using StatusType = Models.PlayerModels.StatusType;

namespace Models.EffectCommands
{
    // 실행 우선순위 정의 (낮은 숫자가 먼저 실행됨)
    public enum CommandPriority
    {
        ManaAndSystem = 0,     // 마나 증감, 턴 종료 등 시스템적 처리
        CardMovement = 10,     // 드로우, 버리기, 덱 섞기 등 카드 위치 이동
        StatusApply = 20,      // 버프 및 디버프 부여 (예: 잔불의 발화 적용)
        DamageAndHeal = 30,    // 기본 데미지 계산 및 회복 (만조, 박동)
        StatusDetonate = 40,   // 상태이상 소모 및 기폭 (예: 홍련)
        CombatAction = 50,
        LateSystem = 100       // 턴 즉시 종료 등 극후반 처리 (예: 대격변)
    }
    
    public abstract class EffectCommand : IComparable<EffectCommand>
    {
        protected PlayerModel target;
        public virtual CommandPriority Priority => CommandPriority.DamageAndHeal;

        public EffectCommand(PlayerModel target)
        {
            this.target = target;
        }
        public abstract void Execute(float multiplier = 1.0f); //multiplier는 llm 평가 받고 곱해지는 계수 자료형은 미정
        
        // C# 내장 Sort()를 위한 비교 로직
        public int CompareTo(EffectCommand other)
        {
            if (other == null) return 1;
            return Priority.CompareTo(other.Priority);
        }
    }

    // [1] 데미지 커맨드 (만조, 붕괴, 갈망 등 모든 데미지)
    public class DamageCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.CombatAction;
        private int amount;

        public DamageCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            target.TakeDamage(Mathf.RoundToInt(amount * multiplier)); 
        }
    }

    // [2] 회복 커맨드 (박동, 원초, 영령 등)
    public class HealCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.CombatAction;
        private int amount;

        public HealCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            target.Heal(Mathf.RoundToInt(amount * multiplier)); 
        }
    }

    // [3] 보호막 커맨드 (지맥, 지평, 신념 등)
    public class ShieldCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.StatusApply;
        private PlayerModel target;
        private int amount;

        public ShieldCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            target.AddShield(Mathf.RoundToInt(amount * multiplier)); 
        }
    }

    // [4] 상태 이상 부여 (잔불, 혹한, 충전, 마안 등 모든 버프/디버프)
    public class AddStatusCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.StatusApply;
        private StatusType status;
        private int stack;
        private int duration;

        public AddStatusCommand(PlayerModel target, StatusType status, int stack, int duration) : base(target)
        {
            this.target = target;
            this.status = status;
            this.stack = stack;
            this.duration = duration;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            // 스택(중첩수)에 multiplier를 적용합니다. 지속 턴 수(duration)는 곱하지 않는 것이 기획상 안전합니다.
            target.AddStatus(status, Mathf.RoundToInt(stack * multiplier), duration); 
        }
    }

    // [5] 상태 이상 즉시 조작/기폭 (홍련, 폭주, 화신, 연성 등)
    public class DetonateStatusCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.StatusDetonate;
        private StatusType status;

        public DetonateStatusCommand(PlayerModel target, StatusType status) : base(target)
        {
            this.target = target;
            this.status = status;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            // 기폭/소모 트리거이므로 multiplier의 영향을 받지 않습니다.
            target.ConsumeAllStatus(status); 
        }
    }

    // [6] 카드 이동/조작 커맨드 (창궁, 허공, 감시, 폭염 섞어넣기 등)
    public class CardMovementCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.CardMovement;
        private EffectType moveType; 
        private int count;
        private string specificCardId;

        public CardMovementCommand(PlayerModel target, EffectType moveType, int count, string specificCardId = "") : base(target)
        {
            this.target = target;
            this.moveType = moveType;
            this.count = count;
            this.specificCardId = specificCardId;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            // TODO: PlayerModel 내에 ProcessCardMovement 함수가 아직 없습니다.
            // target.Deck, target.Hand, target.Graveyard 컴포넌트를 조작하는 함수를 PlayerModel에 추가해야 합니다.
            target.ProcessCardMovement(moveType, count, specificCardId); 
            //
        }
    }

    // [7] 마나 조작 커맨드 (명상, 깨우침, 광신도 등)
    public class ManaCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.ManaAndSystem;
        private int amount; 

        public ManaCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            // 마나 조작은 보통 고정값이므로 multiplier를 적용하지 않는 편이 기획 의도에 맞습니다.
            if (amount > 0)
            {
                target.ManaHeal(amount); 
            }
            else
            {
                target.TryUseMana(-amount); 
            }
        }
    }

    // [8] 특수 시스템 제어 (대격변의 턴 즉시 종료 등)
    public class SystemControlCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.ManaAndSystem; 
        private EffectType systemAction;

        public SystemControlCommand(PlayerModel target, EffectType systemAction) : base(target)
        {
            this.systemAction = systemAction;
        }

        public override void Execute(float multiplier = 1.0f) 
        { 
            if(systemAction == EffectType.EndTurnInstantly)
            {
                // TODO: BattleManager.Instance.EndTurn() 등의 턴 강제 종료 로직 구현 필요
            }
        }
    }    
}
