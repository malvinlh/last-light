using System.Collections.Generic;

namespace LastLight.Gameplay.Run
{
    /// <summary>
    /// What happened over the course of a run, collected as it happens so the end screen has
    /// something to say beyond "you won".
    /// </summary>
    public sealed class RunSummary
    {
        private readonly List<string> log = new List<string>();

        public int StagesCleared { get; internal set; }
        public int TurnsTaken { get; internal set; }
        public int CardsAdded { get; internal set; }
        public int CardsUpgraded { get; internal set; }
        public int CardsRemoved { get; internal set; }
        public int LightRemaining { get; internal set; }
        public int FinalDeckSize { get; internal set; }

        /// <summary>Human-readable beats, in order, for the end-of-run screen.</summary>
        public IReadOnlyList<string> Log => log;

        internal void Record(string entry)
        {
            if (!string.IsNullOrEmpty(entry)) log.Add(entry);
        }
    }
}
