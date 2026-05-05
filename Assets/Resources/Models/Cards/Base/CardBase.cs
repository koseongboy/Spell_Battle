using UnityEngine;
using Models.Cards.Interface;
using Models.PlayerModel;

namespace Models.Cards.CardBase
{
    [CreateAssetMenu(fileName = "CardBase", menuName = "Scriptable Objects/CardBase")]
    public abstract class CardBase : ScriptableObject, ICard
    {
        [Header("Card Info")]
        [SerializeField] private int id;
        [SerializeField] private string cardName;
        [SerializeField] private int cost;
        [SerializeField] [TextArea] private string description;

        public int Id => id;
        public string CardName => cardName;
        public int Cost => cost;

        public abstract void Execute(PlayerModel.PlayerModel caster, PlayerModel.PlayerModel target);

    }
}
