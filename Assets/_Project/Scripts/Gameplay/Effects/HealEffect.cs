using System;
using UnityEngine;

namespace LastLight.Gameplay.Effects
{
    /// <summary>Restores Light, clamped to the target's maximum.</summary>
    [Serializable]
    public sealed class HealEffect : CardEffect
    {
        [SerializeField] private EffectTargeting target = EffectTargeting.Self;

        public HealEffect() { }

        public HealEffect(int amount, int upgradedAmount, EffectTargeting target = EffectTargeting.Self)
            : base(amount, upgradedAmount)
        {
            this.target = target;
        }

        public EffectTargeting Target => target;

        public override void Resolve(EffectContext context) =>
            context.Heal(context.Resolve(target), AmountFor(context.Upgraded));

        public override string Describe(bool upgraded) => $"Restore {AmountFor(upgraded)} Light.";
    }
}
