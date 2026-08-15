namespace LastLight.Gameplay.Combat
{
    /// <summary>Result of a single combat. The run layer decides what that means for the run.</summary>
    public enum CombatOutcome
    {
        InProgress = 0,
        Victory = 1,
        Defeat = 2
    }
}
