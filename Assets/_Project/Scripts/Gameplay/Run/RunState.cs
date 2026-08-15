using System.Collections.Generic;
using LastLight.Gameplay.Cards;

namespace LastLight.Gameplay.Run
{
    /// <summary>How a run ended, or that it has not.</summary>
    public enum RunOutcome
    {
        InProgress = 0,
        Victory = 1,
        Defeat = 2
    }

    /// <summary>
    /// Everything that survives from one stage to the next.
    /// </summary>
    /// <remarks>
    /// This is the definition of "a run" in one object: where you are, how much Light you have
    /// left, and which cards you own. Starting a new run constructs a new instance rather than
    /// resetting fields on the old one, which makes stale state impossible by construction
    /// instead of by remembering to clear everything.
    ///
    /// Card instance ids are handed out from here so they are unique within a run and start
    /// from 1 again in the next one - no static counters, nothing to reset between tests.
    /// </remarks>
    public sealed class RunState
    {
        private readonly List<RuntimeCard> deck = new List<RuntimeCard>();
        private int nextCardInstanceId = 1;

        public RunState(int maxLight)
        {
            MaxLight = maxLight;
            Light = maxLight;
            NodeIndex = 0;
            Outcome = RunOutcome.InProgress;
        }

        public int NodeIndex { get; internal set; }
        public int Light { get; internal set; }
        public int MaxLight { get; }
        public RunOutcome Outcome { get; internal set; }
        public RunSummary Summary { get; } = new RunSummary();

        /// <summary>The player's deck for the whole run. Combat borrows it; it never owns it.</summary>
        public IReadOnlyList<RuntimeCard> Deck => deck;

        internal List<RuntimeCard> MutableDeck => deck;

        /// <summary>Mints a new card copy owned by this run.</summary>
        internal RuntimeCard CreateCard(CardDefinition definition, bool upgraded = false) =>
            new RuntimeCard(nextCardInstanceId++, definition, upgraded);
    }
}
