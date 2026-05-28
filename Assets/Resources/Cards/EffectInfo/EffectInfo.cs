using System;
using Cards.CardUIDatas;
using Models.PlayerModels;
using UnityEngine;

namespace Cards.EffectInfos
{
    // 발동 조건
    public enum ConditionType
    {
        None,
        PrevPropertyMatch,    // 이전 주문 속성 일치 (강굽이 등)
        CurrentPropertyMatch, // 현재 주문 속성 일치
        TargetHasStatus,      // 대상이 특정 상태이상 보유 (젖음, 빙결 등)
        TargetHPBelow,        // 체력이 특정 수치 이하
        CasterShieldAbove,    // 보호막이 특정 수치 이상
        ProphecyAbove,        // 예언 스택 특정 수치 이상
        IsComboAttack         // 연타 공격 여부
    }
    
    // 3. 대상 지정
    public enum TargetType
    {
        None,
        Self,           // 시전자
        Enemy,          // 적
        All,            // 피아 구분 없이 모두
        SelfDeck,       
        EnemyDeck,
        SelfHand,
        EnemyHand,
        SelfGraveyard
    }
    
    // 4. 효과 종류 (가장 중요: 기획서의 모든 행동 패턴을 정의)
    public enum EffectType
    {
        None,
        // 데미지 및 회복
        Damage, DamageByShieldPercentage, DamageByConsumedStatus, DamageByPlayedPropertyAmount,
        Heal, HealByDamageTakenThisTurn, 
        
        // 보호막
        GainShield, MultiplyShieldGainAmount, ConsumeShieldForDamage,
        
        // 상태 이상 제어 (발화, 빙결, 응축, 예언 등)
        AddStatus, RemoveStatus, ModifyStatusDuration, TriggerStatusInstantly, 
        MultiplyStatusDamage, MultiplyStatusTickCount, ConsumeStatusForDamage, AddStatusToNextAttack,
        
        // 카드 조작 (드로우, 버림, 복사, 섞기)
        DrawCard, DiscardRandom, DiscardSpecificPosition, ShuffleSpecificCardToDeck,
        ShuffleSelfToDeck, ShuffleGraveToDeck, CopyHandToDeck, StealCardFromHand, DestroyAllHand,
        
        // 코스트 및 마나 제어
        ModifySpellCost, ModifyNextDrawCost, GainMana, RestrictNextTurnMana, ConsumeAllManaForStatus,
        
        // 속성 조작
        ChangeCurrentSpellProperty, AddPropertyCountToSpell,
        
        // 유틸리티 및 특수 룰
        EndTurnInstantly, ApplyDamageReflection, ApplyLifesteal, IncreaseNextDamage
    }
    
    [Serializable]
    public class EffectInfo
    {
        [Header("효과 기본 설정")]
        public EffectType effectType;
        public TargetType targetType;

        [Header("효과 세부 수치")]
        public int value1;                 // 범용 인자 1 (데미지, 스택, 쉴드, 드로우 수 등)
        public int value2;                 // 범용 인자 2 (지속 턴 수, 횟수 등)
        public StatusType statusType;      // 연관 상태이상 (발화, 빙결 등)
        public Property targetProperty;    // 타겟팅/필터링할 속성 (물, 얼음 등)
        public string specificCardId;      // 생성/섞기/복사 할 특정 카드의 고유 ID (예: 폭염, 정적)

        [Header("조건 설정 (선택)")]
        public ConditionType condition;
        public Property conditionProperty; // 조건 검사 시 사용할 속성 (예: 불)
        public StatusType conditionStatus; // 조건 검사 시 사용할 상태이상 (예: 젖음)
        public int conditionThreshold;     // 체력 10 이하, 예언 10 이상 등의 기준값

        [Header("조건 달성 시 수치 오버라이드 (선택)")]
        // 조건이 맞았을 때 value1, value2 대신 아래 수치를 적용 (강굽이, 잔불 등의 효과 처리용)
        public bool useConditionalValues;
        public int conditionalValue1;
        public int conditionalValue2;
        public EffectType conditionalEffectType; // 조건 만족 시 아예 효과 종류가 바뀔 때 사용
    }
}
