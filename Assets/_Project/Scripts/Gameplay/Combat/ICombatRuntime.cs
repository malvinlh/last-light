namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// The complete set of things an effect is able to cause.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="CombatController"/> and reached only through
    /// <see cref="Effects.EffectContext"/>. Naming the surface as an interface makes the
    /// dependency explicit and one-directional: effects depend on this small contract, not on
    /// the controller, and the controller is free to change around them.
    /// </remarks>
    public interface ICombatRuntime
    {
        /// <summary>Runs damage through modifiers and Ward. Returns Light actually lost.</summary>
        int DealDamage(Combatant source, Combatant target, int baseAmount);

        void GainWard(Combatant target, int amount);

        void Heal(Combatant target, int amount);

        void ApplyStatus(Combatant target, StatusType status, int stacks);

        /// <summary>Draws for the player. Returns how many were actually drawn.</summary>
        int Draw(int count);

        void GainFocus(int amount);
    }
}
