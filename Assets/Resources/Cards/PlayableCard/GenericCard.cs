using UnityEngine;
using Cards.PlayableCards;
using Cards.EffectInfos;
using Models.EffectCommands;
using Models.PlayerModels;
using Models.SpellPayloads;

namespace Cards.EffectInfos
{
    [CreateAssetMenu(fileName = "NewGenericCard", menuName = "Cards/Generic Card")]
    public class GenericCard : PlayableCard
    {
        public override void ApplyCardEffects(SpellPayload payload, PlayerModel caster, PlayerModel enemy)
        {
            if (uiData.Effects == null) return;

            foreach (var effect in uiData.Effects)
            {
                // 1. 조건 검사 (팩트 체크: 이전 주문 속성 일치 여부)
                bool isConditionMet = false;
                if (effect.condition == ConditionType.PrevPropertyMatch)
                {
                     // if (payload.LastSpellProperty == effect.conditionProperty)
                     if (true) // TODO : 이전 주문 속성 가져오는 기능
                     {
                          isConditionMet = true;
                     }
                }

                // 2. 최종 수치 결정 (조건 만족 && 오버라이드 사용 시 수치 변경)
                int finalValue1 = (isConditionMet && effect.useConditionalValues) ? effect.conditionalValue1 : effect.value1;
                int finalValue2 = (isConditionMet && effect.useConditionalValues) ? effect.conditionalValue2 : effect.value2;

                // 3. 타겟 결정
                PlayerModel finalTarget = (effect.targetType == TargetType.Enemy) ? enemy : caster;

                // 4. 효과 종류에 따른 Command 매핑
                switch (effect.effectType)
                {
// [1] 데미지
                    case EffectType.Damage:
                    case EffectType.DamageByShieldPercentage: // (수치 연산이 필요하다면 여기서 finalValue1을 미리 계산해서 넘김)
                        payload.AddCommand(new DamageCommand(finalTarget, finalValue1));
                        break;

                    // [2] 회복
                    case EffectType.Heal:
                    case EffectType.HealByDamageTakenThisTurn:
                        payload.AddCommand(new HealCommand(finalTarget, finalValue1));
                        break;

                    // [3] 보호막
                    case EffectType.GainShield:
                        payload.AddCommand(new ShieldCommand(finalTarget, finalValue1));
                        break;

                    // [4] 상태 이상 부여 및 변경 ('분출' 효과 포함)
                    case EffectType.AddStatus:
                    case EffectType.MultiplyStatusDamage: // 분출: 타겟에게 '특정 상태이상 데미지 배수 증가' 디버프를 거는 것으로 처리
                        payload.AddCommand(new AddStatusCommand(finalTarget, effect.statusType, finalValue1, finalValue2));
                        break;

                    // [5] 상태 이상 기폭 및 소모
                    case EffectType.TriggerStatusInstantly:
                    case EffectType.ConsumeStatusForDamage:
                        payload.AddCommand(new DetonateStatusCommand(finalTarget, effect.statusType));
                        break;

                    // [6] 카드 이동 및 덱 조작 (여러 개의 EffectType을 하나의 Command로 묶음)
                    case EffectType.DrawCard:
                    case EffectType.DiscardRandom:
                    case EffectType.ShuffleSpecificCardToDeck:
                    case EffectType.ShuffleGraveToDeck:
                        // finalValue1은 count(장수)로 사용되며, specificCardId는 특정 카드 생성 시 사용됩니다.
                        payload.AddCommand(new CardMovementCommand(finalTarget, effect.effectType, finalValue1, effect.specificCardId));
                        break;

                    // [7] 마나 조작
                    case EffectType.GainMana:
                        payload.AddCommand(new ManaCommand(finalTarget, finalValue1));
                        break;

                    // [8] 특수 시스템 제어
                    case EffectType.EndTurnInstantly:
                        payload.AddCommand(new SystemControlCommand(finalTarget, effect.effectType));
                        break;

                    // 예외 처리
                    default:
                        Debug.LogWarning($"[GenericCard] 아직 매핑되지 않은 EffectType입니다: {effect.effectType}");
                        break;
                }
            }
        }
    }
}