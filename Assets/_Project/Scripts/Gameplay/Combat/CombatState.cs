namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// The readable state of one combat.
    /// </summary>
    /// <remarks>
    /// Everything here is public to read and internal to write. Views and tests can inspect
    /// whatever they need, but the only thing that can *change* combat is the controller in
    /// this assembly - which is the concrete form of the rule that UI requests actions rather
    /// than mutating state.
    /// </remarks>
    public sealed class CombatState
    {
        public CombatState(PlayerCombatant player, EnemyCombatant enemy)
        {
            Player = player;
            Enemy = enemy;
            Phase = CombatPhase.NotStarted;
            Outcome = CombatOutcome.InProgress;
        }

        public PlayerCombatant Player { get; }
        public EnemyCombatant Enemy { get; }

        public CombatPhase Phase { get; internal set; }
        public CombatOutcome Outcome { get; internal set; }

        /// <summary>1 on the first player turn.</summary>
        public int TurnNumber { get; internal set; }

        public int Focus { get; internal set; }
        public int MaxFocus { get; internal set; }

        public bool IsPlayerInputAllowed => Phase == CombatPhase.PlayerAction && Outcome == CombatOutcome.InProgress;
    }
}
