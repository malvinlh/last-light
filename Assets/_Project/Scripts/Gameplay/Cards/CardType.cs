namespace LastLight.Gameplay.Cards
{
    /// <summary>
    /// Broad category of a card. Drives colour coding and card-frame art only -
    /// it deliberately carries no rules of its own, because all behaviour lives
    /// in the card's effect list.
    /// </summary>
    public enum CardType
    {
        Attack = 0,
        Skill = 1
    }
}
