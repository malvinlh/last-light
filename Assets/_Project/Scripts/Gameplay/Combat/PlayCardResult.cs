namespace LastLight.Gameplay.Combat
{
    /// <summary>Why a card could not be played.</summary>
    public enum PlayRejection
    {
        None = 0,
        NotPlayerTurn = 1,
        CardNotInHand = 2,
        NotEnoughFocus = 3,
        InvalidTarget = 4,
        CombatOver = 5
    }

    /// <summary>
    /// The answer to "can I play this, and if not, why not".
    /// </summary>
    /// <remarks>
    /// Returning a reason instead of a bare bool is what lets the UI explain the refusal to the
    /// player rather than silently ignoring the click. The message text lives here so the
    /// wording is identical everywhere it is shown.
    /// </remarks>
    public readonly struct PlayCardResult
    {
        private PlayCardResult(bool success, PlayRejection rejection)
        {
            Success = success;
            Rejection = rejection;
        }

        public bool Success { get; }
        public PlayRejection Rejection { get; }

        public static PlayCardResult Ok() => new PlayCardResult(true, PlayRejection.None);

        public static PlayCardResult Rejected(PlayRejection rejection) => new PlayCardResult(false, rejection);

        /// <summary>Player-facing explanation, suitable for a toast.</summary>
        public string Message => Rejection switch
        {
            PlayRejection.None => string.Empty,
            PlayRejection.NotPlayerTurn => "Not your turn.",
            PlayRejection.CardNotInHand => "That card is not in your hand.",
            PlayRejection.NotEnoughFocus => "Not enough Focus.",
            PlayRejection.InvalidTarget => "No valid target.",
            PlayRejection.CombatOver => "The fight is over.",
            _ => "You cannot play that."
        };
    }
}
