namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// The two status effects in the game. Kept deliberately small: two statuses is the
    /// minimum that produces real card synergy (buff-then-hit, expose-then-hit) without
    /// dragging in a full status framework the scope does not justify.
    /// </summary>
    public enum StatusType
    {
        /// <summary>+1 damage per stack on everything this combatant deals. Lasts the whole combat.</summary>
        Kindled = 0,

        /// <summary>This combatant takes 50% more damage. Loses one stack at the start of its turn.</summary>
        Exposed = 1
    }

    /// <summary>
    /// Player-facing names and explanations for statuses. Single source of truth so the
    /// card text, the status pip tooltip and the combat log all say the same thing.
    /// </summary>
    public static class StatusInfo
    {
        public static string DisplayName(StatusType status) => status switch
        {
            StatusType.Kindled => "Kindled",
            StatusType.Exposed => "Exposed",
            _ => status.ToString()
        };

        public static string Explain(StatusType status) => status switch
        {
            StatusType.Kindled => "Kindled: deal +1 damage per stack. Lasts until the end of combat.",
            StatusType.Exposed => "Exposed: take 50% more damage. Loses 1 stack at the start of its turn.",
            _ => string.Empty
        };

        /// <summary>Whether the status sheds a stack at the start of its owner's turn.</summary>
        public static bool Decays(StatusType status) => status == StatusType.Exposed;
    }
}
