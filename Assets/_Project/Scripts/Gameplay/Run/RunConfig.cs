using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using UnityEngine;

namespace LastLight.Gameplay.Run
{
    /// <summary>A stack of identical starter cards.</summary>
    [Serializable]
    public sealed class StarterDeckEntry
    {
        [SerializeField] private CardDefinition card;
        [SerializeField, Min(1)] private int count = 1;

        public StarterDeckEntry() { }

        public StarterDeckEntry(CardDefinition card, int count)
        {
            this.card = card;
            this.count = count;
        }

        public CardDefinition Card => card;
        public int Count => count;
    }

    /// <summary>
    /// The whole run described as one asset: how much Light you start with, what is in the
    /// starter deck, what can show up as a reward, and the sequence of stops.
    /// </summary>
    /// <remarks>
    /// Having a single authored entry point for the run shape is what keeps the stage system
    /// honest. Nothing in the code knows there are three fights; it knows there is a list.
    /// </remarks>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "Last Light/Run Config")]
    public sealed class RunConfig : ScriptableObject
    {
        [Header("Player")]
        [SerializeField, Min(1)] private int startingLight = 50;
        [SerializeField] private CombatRules combatRules = new CombatRules();

        [Header("Deck")]
        [SerializeField] private List<StarterDeckEntry> starterDeck = new List<StarterDeckEntry>();
        [SerializeField] private List<CardDefinition> rewardPool = new List<CardDefinition>();
        [SerializeField, Min(1)] private int rewardChoiceCount = 3;

        [Header("Shrine")]
        [SerializeField, Min(1)] private int shrineMendAmount = 12;
        [SerializeField, Min(1), Tooltip("A Shrine will refuse to remove a card below this deck size.")]
        private int minimumDeckSize = 5;

        [Header("Run")]
        [SerializeField] private List<RunNodeDefinition> nodes = new List<RunNodeDefinition>();

        public int StartingLight => startingLight;
        public CombatRules CombatRules => combatRules;
        public IReadOnlyList<StarterDeckEntry> StarterDeck => starterDeck;
        public IReadOnlyList<CardDefinition> RewardPool => rewardPool;
        public int RewardChoiceCount => rewardChoiceCount;
        public int ShrineMendAmount => shrineMendAmount;
        public int MinimumDeckSize => minimumDeckSize;
        public IReadOnlyList<RunNodeDefinition> Nodes => nodes;

        /// <summary>Expands the starter deck entries into one definition per physical card.</summary>
        public IEnumerable<CardDefinition> EnumerateStarterDeck()
        {
            for (int i = 0; i < starterDeck.Count; i++)
            {
                StarterDeckEntry entry = starterDeck[i];
                if (entry?.Card == null) continue;

                for (int copy = 0; copy < entry.Count; copy++)
                {
                    yield return entry.Card;
                }
            }
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        public void Configure(int light, CombatRules rules, IEnumerable<StarterDeckEntry> starter,
            IEnumerable<CardDefinition> rewards, IEnumerable<RunNodeDefinition> runNodes,
            int rewardChoices = 3, int mendAmount = 12, int minDeckSize = 5)
        {
            startingLight = light;
            combatRules = rules ?? new CombatRules();
            starterDeck = starter == null ? new List<StarterDeckEntry>() : new List<StarterDeckEntry>(starter);
            rewardPool = rewards == null ? new List<CardDefinition>() : new List<CardDefinition>(rewards);
            nodes = runNodes == null ? new List<RunNodeDefinition>() : new List<RunNodeDefinition>(runNodes);
            rewardChoiceCount = rewardChoices;
            shrineMendAmount = mendAmount;
            minimumDeckSize = minDeckSize;
        }
#endif
    }
}
