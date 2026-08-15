namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// The Lampwright. Created fresh for each combat but seeded with the Light carried over
    /// from the previous stage, which is what makes the run a run rather than three
    /// independent fights.
    /// </summary>
    public sealed class PlayerCombatant : Combatant
    {
        public PlayerCombatant(string name, int maxLight, int currentLight)
            : base(name, maxLight, currentLight)
        {
        }
    }
}
