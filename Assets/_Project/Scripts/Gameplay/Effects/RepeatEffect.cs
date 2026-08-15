using System;
using UnityEngine;

namespace LastLight.Gameplay.Effects
{
    /// <summary>
    /// Resolves another effect several times.
    /// </summary>
    /// <remarks>
    /// The reason this exists rather than a "MultiHitDamageEffect": repeating is orthogonal to
    /// what is being repeated, so composing it keeps multi-hit available to every atom.
    /// It also demonstrates why the effect list is [SerializeReference] - the inner effect is
    /// itself polymorphic and nests inside its parent.
    ///
    /// Repeating matters mechanically because Kindled adds its bonus to each hit, so
    /// "3 damage twice" and "6 damage once" behave differently under a buff. That is the
    /// synergy the reward pool is built around.
    /// </remarks>
    [Serializable]
    public sealed class RepeatEffect : CardEffect
    {
        [SerializeReference] private CardEffect inner;

        public RepeatEffect() { }

        public RepeatEffect(int times, int upgradedTimes, CardEffect inner) : base(times, upgradedTimes)
        {
            this.inner = inner;
        }

        public CardEffect Inner => inner;

        public override void Resolve(EffectContext context)
        {
            if (inner == null) return;

            int times = AmountFor(context.Upgraded);
            for (int i = 0; i < times; i++)
            {
                inner.Resolve(context);
            }
        }

        public override string Describe(bool upgraded)
        {
            if (inner == null) return string.Empty;
            return $"{inner.Describe(upgraded)} Repeat {AmountFor(upgraded)} times in total.";
        }
    }
}
