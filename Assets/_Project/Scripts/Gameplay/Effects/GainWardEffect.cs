using System;
using UnityEngine;

namespace LastLight.Gameplay.Effects
{
    /// <summary>Grants Ward, which absorbs incoming damage until the owner's next turn begins.</summary>
    [Serializable]
    public sealed class GainWardEffect : CardEffect
    {
        [SerializeField] private EffectTargeting target = EffectTargeting.Self;

        public GainWardEffect() { }

        public GainWardEffect(int amount, int upgradedAmount, EffectTargeting target = EffectTargeting.Self)
            : base(amount, upgradedAmount)
        {
            this.target = target;
        }

        public EffectTargeting Target => target;

        public override void Resolve(EffectContext context) =>
            context.GainWard(context.Resolve(target), AmountFor(context.Upgraded));

        public override string Describe(bool upgraded) => $"Gain {AmountFor(upgraded)} Ward.";
    }
}
