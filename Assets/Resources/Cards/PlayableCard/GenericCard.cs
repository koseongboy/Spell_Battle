using UnityEngine;
using Cards.PlayableCards;
using Cards.EffectInfos;
using Models.EffectCommands;
using Models.PlayerModels;
using Models.SpellPayloads;
using UnityEngine.UIElements;

namespace Cards.EffectInfos
{
    [CreateAssetMenu(fileName = "NewGenericCard", menuName = "Cards/Generic Card")]
    public class GenericCard : PlayableCard
    {
        // 실제 발동 로직 (자식 클래스들이 반드시 구현해야 함)
        public override void AddToPayload(SpellPayload payload, PlayerModel caster, PlayerModel enemy)
        {
            if (uiData == null)
            {
                Debug.LogError($"[PlayableCard 에러] {this.name} 카드의 uiData가 인스펙터에 연결되지 않았습니다!");
                return;
            }
            payload.AddWord(Name);
            payload.AddProperty(Prop);
            payload.EnqueuePendingCard(this); // 카드 등록만 수행
        }
        
        public override void ApplyCardEffects(SpellPayload payload, PlayerModel caster, PlayerModel enemy)
        {
            if (uiData.Effects == null) return;

            foreach (var effect in uiData.Effects)
            {
                // 1. 조건 검사 (이전 주문 속성 일치 여부)
                bool isConditionMet = false;
                if (effect.condition == ConditionType.PrevPropertyMatch)
                {
                     if (caster.LastProperty.Value == effect.conditionProperty) {
                         isConditionMet = true;
                     }
                }

                // 2. 최종 수치 결정 (조건 만족 && 오버라이드 사용 시 수치 변경)
                int finalValue1 = (isConditionMet && effect.useConditionalValues) ? effect.conditionalValue1 : effect.value1;
                int finalValue2 = (isConditionMet && effect.useConditionalValues) ? effect.conditionalValue2 : effect.value2;

                // 3. 타겟 결정
                PlayerModel finalTarget = (effect.targetType == TargetType.Enemy) ? enemy : caster;

                // 4. 효과 종류에 따른 Command 매핑
                switch (effect.effectType) {
                    // [1] 데미지
                    case EffectType.Damage:
                        payload.AddCommand(new DamageCommand(finalTarget, finalValue1));
                        break;

                    case EffectType.DamageByShieldPercentage: 
                        // 팩트: 실행 시점의 보호막을 깎아 데미지를 주기 위해 동적 커맨드로 변경 위임
                        payload.AddCommand(new DynamicDamageCommand(finalTarget, DynamicValueType.CurrentShield, finalValue1 / 100f));
                        break;

                    // [2] 회복
                    case EffectType.Heal:
                        payload.AddCommand(new HealCommand(finalTarget, finalValue1));
                        break;

                    // [3] 보호막
                    case EffectType.GainShield:
                        payload.AddCommand(new ShieldCommand(finalTarget, finalValue1));
                        break;

                    // [4] 상태 이상 부여 및 세두 조작
                    case EffectType.AddStatus:
                        payload.AddCommand(new AddStatusCommand(finalTarget, effect.statusType, finalValue1, finalValue2));
                        break;

                    case EffectType.ModifyStatusDuration:
                        // 대상의 발화 스택 등 지속시간을 증가시키는 기획 대응
                        payload.AddCommand(new ManipulateStatusCommand(finalTarget, effect.statusType, StatusActionType.ExtendDuration, finalValue1));
                        break;

                    case EffectType.MultiplyStatusDamage:
                        // 스택 혹은 데미지 배수를 증가시키는 기획 대응
                        payload.AddCommand(new ManipulateStatusCommand(finalTarget, effect.statusType, StatusActionType.DoubleStacks));
                        break;

                    // [5] 상태 이상 즉시 기폭 및 소모
                    case EffectType.TriggerStatusInstantly:
                        // 소모 없이 즉시 효과만 1회 발동하는 기획 대응
                        payload.AddCommand(new ManipulateStatusCommand(finalTarget, effect.statusType, StatusActionType.TriggerOnce));
                        break;

                    case EffectType.ConsumeStatusForDamage:
                        // 기존의 모든 스택 소모/기폭 커맨드
                        payload.AddCommand(new DetonateStatusCommand(finalTarget, effect.statusType));
                        break;

                    // [6] 코스트 조작 커맨드 매핑 추가
                    case EffectType.ModifySpellCost:
                        payload.AddCommand(new ModifyCostCommand(finalTarget, effect.targetType, finalValue1));
                        break;

                    // [7] 카드 이동 및 덱 조작
                    case EffectType.DrawCard:
                    case EffectType.DiscardRandom:
                    case EffectType.ShuffleSpecificCardToDeck:
                    case EffectType.ShuffleGraveToDeck:
                        payload.AddCommand(new CardMovementCommand(finalTarget, effect.effectType, finalValue1, effect.specificCardId));
                        break;

                    // [8] 마나 조작
                    case EffectType.GainMana:
                        payload.AddCommand(new ManaCommand(finalTarget, finalValue1));
                        break;

                    // [9] 특수 시스템 제어
                    case EffectType.EndTurnInstantly:
                        payload.AddCommand(new SystemControlCommand(finalTarget, effect.effectType));
                        break;

                    default:
                        Debug.LogWarning($"[GenericCard] 아직 매핑되지 않은 EffectType입니다: {effect.effectType}");
                        break;
                }
            }
        }
    }
    
    public enum CardKeyword {
        None = 0,
        Ignite,      // 발화
        Riverbend,   // 강굽이
        Freeze,      // 빙결
        Prophecy,    // 예언
        Condense,    // 응축
        Reflect,      // 반사
        Expose,     // 방출
        Wet,        // 젖음 (water)
        Stun,       // 스턴 (1턴 쉬기)
        Smash,      // 깨뜨림 (빙결 3스택)
        Critical,       // 치명타
        OverCharge,      // 과충전
        Drain,       // 생명력 흡수
        Ultimate        // 궁극기
    }
}