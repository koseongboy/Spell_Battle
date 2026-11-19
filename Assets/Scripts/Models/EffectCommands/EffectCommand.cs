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

    // 커맨드 실행 우선순위 (값이 낮을수록 먼저 실행)
    // 카드 영창 순서와 무관하게 "상태 부여 → 피해 → 기폭" 같은 일관된 해석 순서를 보장한다.
    public enum CommandPriority
    {
        ManaAndSystem = 0,     // 마나 증감, 코스트 변경 등 시스템 처리
        CardMovement = 10,     // 드로우, 버리기, 덱 섞기 등 카드 위치 이동
        StatusApply = 20,      // 버프/디버프, 보호막 부여 (예: 잔불의 발화)
        DamageAndHeal = 30,    // 현재 상태를 참조하는 동적 피해 (DynamicDamageCommand)
        StatusDetonate = 40,   // 상태이상 소모·기폭·조작 (예: 홍련)
        CombatAction = 50,     // 고정 수치 피해 및 회복
        LateSystem = 100       // 턴 즉시 종료 등 마지막에 처리할 효과 (예: 대격변)
    }

    /// <summary>
    /// 카드 효과 하나를 표현하는 커맨드 (Command 패턴).
    /// SpellPayload가 카드들로부터 커맨드를 수집해 Priority 순으로 정렬한 뒤,
    /// 서버에서 ExecuteRoutine으로 VFX 연출과 실제 수치 적용을 순차 실행한다.
    /// </summary>
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

        /// <summary>
        /// [서버] VFX 연출 후 효과를 적용한다.
        /// 게스트에게는 RPC로 연출을 지시하고, 호스트는 연출이 끝날 때까지 대기해 화면과 수치 변화 시점을 맞춘다.
        /// </summary>
        public virtual IEnumerator ExecuteRoutine(float multiplier = 1.0f)
        {
            if (MyVFXType != VFXType.None)
            {
                Controllers.SpellControllers.SpellController.Instance.PlayVisualEffectClientRpc(MyVFXType, RelatedStatus, target.NetworkObjectId);

                yield return Managers.VFX.BattleVFXManager.Instance.PlayVFXRoutine(MyVFXType, RelatedStatus, target);

            }

            Execute(multiplier);
        }

        /// <param name="multiplier">AI 영창 평가 점수로부터 환산된 효과 배율</param>
        protected abstract void Execute(float multiplier = 1.0f);

        // List.Sort()에서 Priority 오름차순 정렬에 사용
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
            // 배율은 스택에만 적용하고 지속 턴 수는 고정 (턴 수가 늘어나면 밸런스가 급격히 무너짐)
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
            // 소모 트리거이므로 배율 미적용
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
        // 카드 이동은 파티클 VFX 대신 짧은 대기 후 바로 적용
        public override IEnumerator ExecuteRoutine(float multiplier = 1.0f)
        {
            // TODO: DOTween 기반 카드 이동 애니메이션
            yield return new WaitForSeconds(0.1f);

            Execute(multiplier);
        }


        protected override void Execute(float multiplier = 1.0f)
        {
            // 덱/손패/무덤 간 실제 이동과 클라이언트 동기화는 PlayerModel에서 처리
            target.ProcessCardMovement(moveType, count, specificCardId);
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
            // 마나는 고정 자원이므로 배율 미적용 (양수: 회복, 음수: 소모)
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
                // TODO: PhaseManager를 통한 턴 강제 종료 연동
            }
        }
    }    
    
    // 실행 시점의 게임 상태를 참조해 피해량을 산출하는 기준값
    public enum DynamicValueType {
        None,
        CurrentShield,         // 현재 보호막
        WaterSpellsCast,       // 이번 게임에 사용한 물 속성 주문 수
        ConsumedStatusStack,   // 방금 소모한 상태이상 중첩 수
        MissingHealth          // 잃은 체력
    }

    // [9] 동적 피해 커맨드: 생성 시점이 아닌 실행 시점의 상태로 피해량을 계산
    public class DynamicDamageCommand : EffectCommand {
        public override CommandPriority Priority => CommandPriority.DamageAndHeal;
        public override VFXType MyVFXType => VFXType.Damage;
        private DynamicValueType valueType;
        private float ratio; // 기준값 대비 비율 (30% → 0.3f)

        public DynamicDamageCommand(PlayerModel target, DynamicValueType valueType, float ratio) : base(target) {
            this.valueType = valueType;
            this.ratio = ratio;
        }

        protected override void Execute(float multiplier = 1.0f) {
            int calculatedAmount = 0;

            switch (valueType) {
                case DynamicValueType.CurrentShield:
                    calculatedAmount = Mathf.RoundToInt(target.Shield.Value * ratio);
                    break;
                case DynamicValueType.WaterSpellsCast:
                    // TODO: 게임 중 사용한 물 속성 주문 수 집계 연동
                    calculatedAmount = Mathf.RoundToInt(10 /* 임시값 */ * ratio); 
                    break;
            }

            target.TakeDamage(Mathf.RoundToInt(calculatedAmount * multiplier));
        }
    }
    
    // [10] 카드 코스트 변경 커맨드
    public class ModifyCostCommand : EffectCommand {
        public override CommandPriority Priority => CommandPriority.ManaAndSystem;
        public override VFXType MyVFXType => amount > 0 ? VFXType.CostUp : VFXType.CostDown;
        private TargetType cardTargetLocation; // 적용 위치 (손패, 덱, 다음 드로우 등)
        private int amount;
        private bool isSetToZero; // true면 amount 대신 코스트를 0으로 고정

        public ModifyCostCommand(PlayerModel target, TargetType location, int amount, bool isSetToZero = false) : base(target) {
            this.cardTargetLocation = location;
            this.amount = amount;
            this.isSetToZero = isSetToZero;
        }

        protected override void Execute(float multiplier = 1.0f) {
            // TODO: 대상 위치(손패/덱)의 카드 코스트 수정 로직 구현
        }
    }

    // 기존 상태이상에 대한 조작 방식
    public enum StatusActionType {
        ExtendDuration, // 지속시간 연장
        TriggerOnce,    // 스택 소모 없이 1회 강제 발동
        DoubleStacks    // 현재 스택 2배
    }

    // [11] 상태이상 조작 커맨드 (지속시간 연장, 강제 발동, 스택 증폭)
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
