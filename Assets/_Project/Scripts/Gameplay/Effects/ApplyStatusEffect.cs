using System;
using LastLight.Gameplay.Combat;
using UnityEngine;

namespace LastLight.Gameplay.Effects
{
    /// <summary>
    /// Applies stacks of a status. This is the atom that makes card synergy possible:
    /// Kindled on yourself before a multi-hit attack, or Exposed on the enemy before a big one.
    /// </summary>
    [Serializable]
    public sealed class ApplyStatusEffect : CardEffect
    {
        [SerializeField] private StatusType status = StatusType.Kindled;
        [SerializeField] private EffectTargeting target = EffectTargeting.Self;

        public ApplyStatusEffect() { }

        public ApplyStatusEffect(StatusType status, int stacks, int upgradedStacks, EffectTargeting target)
            : base(stacks, upgradedStacks)
        {
            this.status = status;
            this.target = target;
        }

        public StatusType Status => status;
        public EffectTargeting Target => target;

        public override void Resolve(EffectContext context) =>
            context.ApplyStatus(context.Resolve(target), status, AmountFor(context.Upgraded));

        public override string Describe(bool upgraded)
        {
            int stacks = AmountFor(upgraded);
            string name = StatusInfo.DisplayName(status);
            return target == EffectTargeting.Self
                ? $"Gain {stacks} {name}."
                : $"Apply {stacks} {name}.";
        }

        public override string Signature() => $"{base.Signature()}:{status}->{target}";
    }
}
