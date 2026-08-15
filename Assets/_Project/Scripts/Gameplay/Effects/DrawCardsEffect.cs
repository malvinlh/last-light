using System;

namespace LastLight.Gameplay.Effects
{
    /// <summary>Draws cards for the player. Reshuffling is handled by the deck.</summary>
    [Serializable]
    public sealed class DrawCardsEffect : CardEffect
    {
        public DrawCardsEffect() { }

        public DrawCardsEffect(int amount, int upgradedAmount) : base(amount, upgradedAmount) { }

        public override void Resolve(EffectContext context) => context.Draw(AmountFor(context.Upgraded));

        public override string Describe(bool upgraded)
        {
            int count = AmountFor(upgraded);
            return count == 1 ? "Draw 1 card." : $"Draw {count} cards.";
        }
    }
}
