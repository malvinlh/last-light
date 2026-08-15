using System;
using UnityEngine;

namespace LastLight.Gameplay.Effects
{
    /// <summary>Deals damage. Modifiers (Kindled, Exposed) and Ward are applied by the combat layer.</summary>
    [Serializable]
    public sealed class DealDamageEffect : CardEffect
    {
        [SerializeField] private EffectTargeting target = EffectTargeting.Opponent;

        public DealDamageEffect() { }

        public DealDamageEffect(int amount, int upgradedAmount, EffectTargeting target = EffectTargeting.Opponent)
            : base(amount, upgradedAmount)
        {
            this.target = target;
        }

        public EffectTargeting Target => target;

        public override void Resolve(EffectContext context) =>
            context.DealDamage(context.Resolve(target), AmountFor(context.Upgraded));

        public override string Describe(bool upgraded) => target == EffectTargeting.Self
            ? $"Take {AmountFor(upgraded)} damage."
            : $"Deal {AmountFor(upgraded)} damage.";

        public override string Signature() => $"{base.Signature()}->{target}";
    }
}
