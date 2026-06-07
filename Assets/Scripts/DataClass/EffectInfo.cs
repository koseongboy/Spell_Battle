using System;
using Cards.CardUIDatas;
using Models.PlayerModels;
using UnityEngine;

namespace Cards.EffectInfos {
    // 1. 발동 조건
    public enum ConditionType {
        None,
        PrevPropertyMatch,    
        CurrentPropertyMatch, 
        TargetHasStatus,      
        TargetHPBelow,        
        CasterHPBelow,        // 🌟 추가됨: 시전자 체력이 특정 수치 이하 (예: 지핵 카드의 "내 체력이 10 이하일 때")
        CasterShieldAbove,    
        ProphecyAbove,        
        IsComboAttack         
    }
    
    // 2. 대상 지정 (완벽합니다)
    public enum TargetType {
        None,
        Self,           
        Enemy,          
        All,            
        SelfDeck,       
        EnemyDeck,
        SelfHand,
        EnemyHand,
        SelfGraveyard
    }
    
    // 3. 효과 종류
    public enum EffectType {
        None,
        // 데미지 및 회복
        Damage, DamageByShieldPercentage, DamageByConsumedStatus, DamageByPlayedPropertyAmount,
        Heal, HealByDamageTakenThisTurn, 
        
        // 보호막
        GainShield, MultiplyShieldGainAmount, ConsumeShieldForDamage,
        
        // 상태 이상 제어 
        AddStatus, RemoveStatus, ModifyStatusDuration, TriggerStatusInstantly, 
        MultiplyStatusDamage, MultiplyStatusTickCount, ConsumeStatusForDamage, AddStatusToNextAttack,
        
        // 카드 조작
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
    public class EffectInfo {
        [Header("효과 기본 설정")]
        public EffectType effectType;
        public TargetType targetType;

        [Header("효과 세부 수치")]
        public int value1;                 
        public int value2;                 
        public StatusType statusType;      
        public Property targetProperty;    
        public string specificCardId;      
        
        // 🌟 추가됨: 증감 연산(+/-)이 아니라 특정 값으로 아예 고정할 때 사용하는 플래그 
        // (예: isAbsoluteValue = true, value1 = 0 이면 "코스트가 0이 됩니다"로 해석)
        public bool isAbsoluteValue;       

        [Header("조건 설정 (선택)")]
        public ConditionType condition;
        public Property conditionProperty; 
        public StatusType conditionStatus; 
        public int conditionThreshold;     

        [Header("조건 달성 시 수치 오버라이드 (선택)")]
        public bool useConditionalValues;
        public int conditionalValue1;
        public int conditionalValue2;
        public EffectType conditionalEffectType; 
    }
}