using UnityEngine;
using Models.PlayerModel;
using NUnit.Framework.Internal.Commands;
using UnityEngine.UIElements;

namespace Models.Cards.Interface
{
    public interface ICard
    {
        string CardName {get;}
        int Cost {get;}
        
        void Execute(PlayerModel.PlayerModel caster, PlayerModel.PlayerModel target);
        
    }

    public interface IAttackCard : ICard
    {
        int Damage {get;}
    }

    public interface IDefenceCard : ICard
    {
        
        int HealAmount {get;}
        int ShieldAmount {get;}
    }

    public interface IChanedCard : ICard
    {
        void Chained(); //쓰려나? (todo)
    }

    public interface IFireCard : ICard
    {
        int IgniteStacks {get;}
        void TriggerIgnite(PlayerModel.PlayerModel target);
    }

    public interface IWaterCard : ICard
    {
        int WaterChainStacks {get;}
        void WaterChained(PlayerModel.PlayerModel target);
    }

    public interface IGroundCard : ICard
    {}

    public interface IWindCard : ICard
    {}

    public interface IThunderCard : ICard
    {
        void Ignited();

        void WaterChained();

        void Iced();
    }

    public interface IIceCard : ICard
    {
        int IceStacks {get;}
        void TriggerIce();
    }

    public interface IVoidCard : ICard
    {
        int ProphecyStacks {get;}
        void TriggerPropecyStacks();
    }

    public interface IVisionCard : ICard
    {
        int CondensationStacks {get;}
        void TriggerCondensationStacks();

        void Emission();
    }

    public interface ILifeCard : ICard
    {
        
    }

    public interface IImmediateCard: ICard
    {
        //(todo) 즉발마법 기획이 끝나야할듯
    }
}
