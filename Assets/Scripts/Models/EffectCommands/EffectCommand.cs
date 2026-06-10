using System;
using Cards.EffectInfos;
using UnityEngine;
using Models.PlayerModels;
using Cards.PlayableCards;
using StatusType = Models.PlayerModels.StatusType;
using System.Collections;

namespace Models.EffectCommands
{
    public enum VFXType {
        None, Damage, DynamicDamage, Heal, Shield, AddStatus, DetonateStatus, CardMovement, ManaGain, ManaLoss, SystemControl, CostUp, CostDown
    }
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
        public virtual VFXType MyVFXType => VFXType.None;
        public virtual StatusType RelatedStatus => StatusType.None;
        public EffectCommand(PlayerModel target)
        {
            this.target = target;
        }

        public virtual IEnumerator ExecuteRoutine(float multiplier = 1.0f)
        {
            // 1. 내가 가진 명찰과 타겟 정보를 바탕으로 매니저에게 VFX 재생을 '요청'합니다.
            if (MyVFXType != VFXType.None)
            {
                Controllers.SpellControllers.SpellController.Instance.PlayVisualEffectClientRpc(MyVFXType, RelatedStatus, target.NetworkObjectId);

                yield return Managers.VFX.BattleVFXManager.Instance.PlayVFXRoutine(MyVFXType, RelatedStatus, target);
            }

            // 2. 이펙트 재생(1.5초 대기)이 끝나면, 나만의 실제 데미지/힐 로직을 조용히 실행합니다.
            Execute(multiplier);
        }
        
        protected abstract void Execute(float multiplier = 1.0f); //multiplier는 llm 평가 받고 곱해지는 계수 자료형은 미정
        
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
        public override VFXType MyVFXType => VFXType.Damage;

        public DamageCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        protected override void Execute(float multiplier = 1.0f) 
        { 
            target.TakeDamage(Mathf.RoundToInt(amount * multiplier)); 
        }
    }

    // [2] 회복 커맨드 (박동, 원초, 영령 등)
    public class HealCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.CombatAction;
        private int amount;
        public override VFXType MyVFXType => VFXType.Heal;

        public HealCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        protected override void Execute(float multiplier = 1.0f) 
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
        public override VFXType MyVFXType => VFXType.Shield;

        public ShieldCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        protected override void Execute(float multiplier = 1.0f) 
        { 
            target.AddShield(Mathf.RoundToInt(amount * multiplier)); 
        }
    }

    // [4] 상태 이상 부여 (잔불, 혹한, 충전, 마안 등 모든 버프/디버프)
    public class AddStatusCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.StatusApply;
        private StatusType status;
        public override VFXType MyVFXType => VFXType.AddStatus;
        public override StatusType RelatedStatus => status;
        private int stack;
        private int duration;

        public AddStatusCommand(PlayerModel target, StatusType status, int stack, int duration) : base(target)
        {
            this.target = target;
            this.status = status;
            this.stack = stack;
            this.duration = duration;
        }

        protected override void Execute(float multiplier = 1.0f) 
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
        public override VFXType MyVFXType => VFXType.DetonateStatus;

        public DetonateStatusCommand(PlayerModel target, StatusType status) : base(target)
        {
            this.target = target;
            this.status = status;
        }

        protected override void Execute(float multiplier = 1.0f) 
        { 
            // 기폭/소모 트리거이므로 multiplier의 영향을 받지 않습니다.
            target.ConsumeAllStatus(status); 
        }
    }

    // [6] 카드 이동/조작 커맨드 (창궁, 허공, 감시, 폭염 섞어넣기 등)
    public class CardMovementCommand : EffectCommand
    {
        public override CommandPriority Priority => CommandPriority.CardMovement;
        public override VFXType MyVFXType => VFXType.CardMovement;
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
        public override IEnumerator ExecuteRoutine(float multiplier = 1.0f)
        {
            //todo DoTween을 통한 카드 이동 애니매이션 구현 요함
            yield return new WaitForSeconds(0.1f);

            // 2. 이펙트 재생(1.5초 대기)이 끝나면, 나만의 실제 데미지/힐 로직을 조용히 실행합니다.
            Execute(multiplier);
        }


        protected override void Execute(float multiplier = 1.0f) 
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
        public override VFXType MyVFXType => amount > 0 ? VFXType.ManaGain : VFXType.ManaLoss;
        public ManaCommand(PlayerModel target, int amount) : base(target)
        {
            this.target = target;
            this.amount = amount;
        }

        protected override void Execute(float multiplier = 1.0f) 
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
        public override VFXType MyVFXType => VFXType.SystemControl;

        public SystemControlCommand(PlayerModel target, EffectType systemAction) : base(target)
        {
            this.systemAction = systemAction;
        }

        protected override void Execute(float multiplier = 1.0f) 
        { 
            if(systemAction == EffectType.EndTurnInstantly)
            {
                // TODO: BattleManager.Instance.EndTurn() 등의 턴 강제 종료 로직 구현 필요
            }
        }
    }    
    
    // 🌟 동적으로 계산할 값의 종류를 정의
    public enum DynamicValueType {
        None,
        CurrentShield,         // 현재 보호막
        WaterSpellsCast,       // 이번 게임에 사용한 물 속성 주문 수
        ConsumedStatusStack,   // 방금 소모한 상태이상 중첩 수
        MissingHealth          // 잃은 체력
    }

    public class DynamicDamageCommand : EffectCommand {
        public override CommandPriority Priority => CommandPriority.DamageAndHeal;
        public override VFXType MyVFXType => VFXType.Damage;
        private DynamicValueType valueType;
        private float ratio; // 예: 30%면 0.3f

        public DynamicDamageCommand(PlayerModel target, DynamicValueType valueType, float ratio) : base(target) {
            this.valueType = valueType;
            this.ratio = ratio;
        }

        protected override void Execute(float multiplier = 1.0f) {
            int calculatedAmount = 0;
            
            // 실행되는 순간의 타겟 상태를 읽어와서 계산
            switch (valueType) {
                case DynamicValueType.CurrentShield:
                    calculatedAmount = Mathf.RoundToInt(target.Shield.Value * ratio);
                    break;
                case DynamicValueType.WaterSpellsCast:
                    // TODO: BattleManager 등에서 카운트 가져오기
                    calculatedAmount = Mathf.RoundToInt(10 /* 임시값 */ * ratio); 
                    break;
            }

            target.TakeDamage(Mathf.RoundToInt(calculatedAmount * multiplier));
        }
    }
    
    public class ModifyCostCommand : EffectCommand {
        public override CommandPriority Priority => CommandPriority.ManaAndSystem;
        public override VFXType MyVFXType => amount > 0 ? VFXType.CostUp : VFXType.CostDown;
        private TargetType cardTargetLocation; // 핸드, 덱, 다음 드로우 등
        private int amount;
        private bool isSetToZero; // 코스트를 아예 0으로 만드는 경우

        public ModifyCostCommand(PlayerModel target, TargetType location, int amount, bool isSetToZero = false) : base(target) {
            this.cardTargetLocation = location;
            this.amount = amount;
            this.isSetToZero = isSetToZero;
        }

        protected override void Execute(float multiplier = 1.0f) {
            // TODO: PlayerModel.Deck이나 Hand에 접근하여 조건에 맞는 카드의 Cost를 직접 수정하는 로직 구현
        }
    }
    
    public enum StatusActionType {
        ExtendDuration, // 지속시간 연장
        TriggerOnce,    // 스택 소모 없이 1회 강제 발동
        DoubleStacks    // 현재 스택 2배
    }

    public class ManipulateStatusCommand : EffectCommand {
        public override CommandPriority Priority => CommandPriority.StatusDetonate;
        public override VFXType MyVFXType => VFXType.AddStatus;
        public override StatusType RelatedStatus => status;
        private StatusType status;
        private StatusActionType actionType;
        private int value;

        public ManipulateStatusCommand(PlayerModel target, StatusType status, StatusActionType actionType, int value = 0) : base(target) {
            this.status = status;
            this.actionType = actionType;
            this.value = value;
        }

        protected override void Execute(float multiplier = 1.0f) {
            switch(actionType) {
                case StatusActionType.ExtendDuration:
                    target.ExtendStatusDuration(status, value);
                    break;
                case StatusActionType.TriggerOnce:
                    target.TriggerStatusEffect(status);
                    break;
                case StatusActionType.DoubleStacks:
                    target.MultiplyStatusStack(status, 2);
                    break;
            }
        }
    }
}
