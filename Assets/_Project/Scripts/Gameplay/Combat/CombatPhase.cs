namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// The turn cycle, written out explicitly rather than implied by a pile of booleans.
    /// </summary>
    /// <remarks>
    /// The loop is:
    /// CombatStart -> PlayerTurnStart -> PlayerAction -> PlayerTurnEnd -> EnemyTurn
    /// -> ResolveCheck -> back to PlayerTurnStart, until ResolveCheck reaches CombatEnd.
    ///
    /// <see cref="PlayerAction"/> is the only phase in which a card can be played, and every
    /// other phase passes through in a single synchronous step. Making the phase an explicit
    /// value means "is the player allowed to act right now" is one comparison rather than a
    /// judgement call spread across the UI.
    /// </remarks>
    public enum CombatPhase
    {
        NotStarted = 0,
        CombatStart = 1,
        PlayerTurnStart = 2,
        PlayerAction = 3,
        PlayerTurnEnd = 4,
        EnemyTurn = 5,
        ResolveCheck = 6,
        CombatEnd = 7
    }
}
