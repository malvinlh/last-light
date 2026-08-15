using System;

namespace LastLight.Gameplay.Effects
{
    /// <summary>Refunds Focus this turn, letting a cheap card enable an expensive one.</summary>
    [Serializable]
    public sealed class GainFocusEffect : CardEffect
    {
        public GainFocusEffect() { }

        public GainFocusEffect(int amount, int upgradedAmount) : base(amount, upgradedAmount) { }

        public override void Resolve(EffectContext context) => context.GainFocus(AmountFor(context.Upgraded));

        public override string Describe(bool upgraded) => $"Gain {AmountFor(upgraded)} Focus.";
    }
}
