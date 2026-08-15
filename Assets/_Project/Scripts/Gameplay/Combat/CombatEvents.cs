using LastLight.Gameplay.Cards;

namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// One packet of damage, after modifiers and Ward.
    /// </summary>
    /// <remarks>
    /// Carries enough detail for the view to animate the hit without asking the combat layer
    /// any follow-up questions - including how much Ward soaked, so a fully blocked hit can be
    /// shown as a block rather than a zero.
    /// </remarks>
    public readonly struct DamageEvent
    {
        public DamageEvent(Combatant source, Combatant target, int amountAfterModifiers, int lightLost, int wardAbsorbed)
        {
            Source = source;
            Target = target;
            AmountAfterModifiers = amountAfterModifiers;
            LightLost = lightLost;
            WardAbsorbed = wardAbsorbed;
        }

        public Combatant Source { get; }
        public Combatant Target { get; }
        public int AmountAfterModifiers { get; }
        public int LightLost { get; }
        public int WardAbsorbed { get; }

        public bool FullyBlocked => LightLost == 0 && WardAbsorbed > 0;
    }

    /// <summary>A card that resolved successfully.</summary>
    public readonly struct CardPlayedEvent
    {
        public CardPlayedEvent(RuntimeCard card, int focusSpent)
        {
            Card = card;
            FocusSpent = focusSpent;
        }

        public RuntimeCard Card { get; }
        public int FocusSpent { get; }
    }
}
