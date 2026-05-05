using Models.Cards.CardBase;
using UnityEngine;
using Models.Cards.Interface;
using Models.PlayerModel;

namespace Models.Cards.Implementations
{
    [CreateAssetMenu(fileName = "New Ignite Card", menuName = "Cards/Fire/Ignite")]
    public class IgniteCard : CardBase.CardBase, IFireCard
    {
        [Header("Fire Properties")]
        [SerializeField] private int igniteStacks = 1; // 기획서 기준: 2턴 동안 발화 1스택
        
        // IFireCard 인터페이스 구현부
        public int IgniteStacks => igniteStacks;

        // ICard 인터페이스(CardBase)의 핵심 실행부 구현
        public override void Execute(PlayerModel.PlayerModel caster, PlayerModel.PlayerModel target)
        {
            Debug.Log($"[{CardName}] 카드 발동! 대상에게 발화 {IgniteStacks} 스택을 부여합니다.");
            
            // 지난 번에 만든 PlayerModel의 AddStatus 호출!
            // (StatusType.Ignite, 스택 수, 지속 턴 수)
            target.AddStatus(StatusType.Ignite, IgniteStacks, 2); 
        }


        public void TriggerIgnite(PlayerModel.PlayerModel target)
        {
            // 발화 카드는 아무것도 안함.
        }
    }
}
