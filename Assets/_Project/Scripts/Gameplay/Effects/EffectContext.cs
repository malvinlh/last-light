using LastLight.Gameplay.Combat;

namespace LastLight.Gameplay.Effects
{
    /// <summary>
    /// Everything an effect is allowed to see and do, and nothing else.
    /// </summary>
    /// <remarks>
    /// This is the seam between "what a card does" and "how combat works". Effects never get
    /// a reference to the combat controller, the deck, or the UI - they get this, and the
    /// verbs on it are the complete list of things an effect can cause. Adding a new verb is
    /// a deliberate act, which keeps the effect layer from quietly growing tendrils into the
    /// rest of the game.
    /// </remarks>
    public sealed class EffectContext
    {
        private readonly ICombatRuntime runtime;

        public EffectContext(ICombatRuntime runtime, Combatant source, Combatant opponent, bool upgraded)
        {
            this.runtime = runtime;
            Source = source;
            Opponent = opponent;
            Upgraded = upgraded;
        }

        /// <summary>The combatant performing the effect.</summary>
        public Combatant Source { get; }

        /// <summary>The combatant on the other side of the table from <see cref="Source"/>.</summary>
        public Combatant Opponent { get; }

        /// <summary>Whether the copy being played is upgraded. Effects read their magnitude through this.</summary>
        public bool Upgraded { get; }

        public Combatant Resolve(EffectTargeting targeting) =>
            targeting == EffectTargeting.Self ? Source : Opponent;

        public int DealDamage(Combatant target, int amount) => runtime.DealDamage(Source, target, amount);

        public void GainWard(Combatant target, int amount) => runtime.GainWard(target, amount);

        public void Heal(Combatant target, int amount) => runtime.Heal(target, amount);

        public void ApplyStatus(Combatant target, StatusType status, int stacks) =>
            runtime.ApplyStatus(target, status, stacks);

        public int Draw(int count) => runtime.Draw(count);

        public void GainFocus(int amount) => runtime.GainFocus(amount);
    }
}
