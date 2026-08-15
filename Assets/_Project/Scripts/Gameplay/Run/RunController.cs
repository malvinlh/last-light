using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Common;
using LastLight.Gameplay.Rewards;

namespace LastLight.Gameplay.Run
{
    /// <summary>
    /// Drives a run: walks the node list, carries Light and the deck between stages, and owns
    /// the deck-progression verbs (draft, upgrade, remove).
    /// </summary>
    /// <remarks>
    /// The controller is the only thing that writes to <see cref="RunState"/>. Combat is
    /// borrowed rather than owned - a fresh <see cref="CombatController"/> is built for each
    /// combat node over the run's card list, and when it finishes, its results are folded back
    /// into the run. That direction of flow (run owns the truth, combat borrows it) is why a
    /// card drafted in stage 1 is simply present in stage 2 with no syncing code.
    /// </remarks>
    public sealed class RunController
    {
        private readonly RunConfig config;
        private readonly GameRng rng;
        private readonly List<CardDefinition> currentRewardChoices = new List<CardDefinition>();

        public RunController(RunConfig config, GameRng rng)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public RunConfig Config => config;
        public RunState State { get; private set; }
        public CombatController ActiveCombat { get; private set; }

        /// <summary>True once this node's Shrine has been used, so it grants exactly one boon.</summary>
        public bool ShrineUsed { get; private set; }

        public event Action RunStarted;
        public event Action<RunNodeDefinition> NodeEntered;
        public event Action<RunOutcome> RunEnded;

        public RunNodeDefinition CurrentNode =>
            State != null && State.NodeIndex >= 0 && State.NodeIndex < config.Nodes.Count
                ? config.Nodes[State.NodeIndex]
                : null;

        public bool IsRunOver => State == null || State.Outcome != RunOutcome.InProgress;

        /// <summary>Total stops in the run, for "Stage 2 of 6" style labels.</summary>
        public int NodeCount => config.Nodes.Count;

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>
        /// Throws away everything from the previous run and starts a fresh one. Because the state
        /// object is replaced wholesale, there is no field that can be forgotten here - the
        /// starter deck, Light, node cursor, summary and card ids all come back at their
        /// defaults.
        /// </summary>
        public void StartNewRun()
        {
            State = new RunState(config.StartingLight);
            ActiveCombat = null;
            ShrineUsed = false;
            currentRewardChoices.Clear();

            foreach (CardDefinition definition in config.EnumerateStarterDeck())
            {
                State.MutableDeck.Add(State.CreateCard(definition));
            }

            RunStarted?.Invoke();
            EnterCurrentNode();
        }

        /// <summary>
        /// Moves to the next stop. Running off the end of the node list is how a run is won -
        /// victory is "you survived every node", not a flag set by the last fight.
        /// </summary>
        public void AdvanceToNextNode()
        {
            if (IsRunOver) return;

            State.NodeIndex++;
            ActiveCombat = null;

            if (State.NodeIndex >= config.Nodes.Count)
            {
                EndRun(RunOutcome.Victory);
                return;
            }

            EnterCurrentNode();
        }

        private void EnterCurrentNode()
        {
            ShrineUsed = false;
            currentRewardChoices.Clear();

            RunNodeDefinition node = CurrentNode;
            if (node != null && node.Kind == RunNodeKind.CardReward)
            {
                currentRewardChoices.AddRange(RewardService.Roll(config.RewardPool, config.RewardChoiceCount, rng));
            }

            NodeEntered?.Invoke(node);
        }

        private void EndRun(RunOutcome outcome)
        {
            State.Outcome = outcome;
            State.Summary.LightRemaining = State.Light;
            State.Summary.FinalDeckSize = State.Deck.Count;
            RunEnded?.Invoke(outcome);
        }

        // ---------------------------------------------------------------- combat nodes

        /// <summary>
        /// Builds and starts the combat for the current node. Returns null if the current node
        /// is not a combat, so a mis-routed screen fails visibly rather than half-working.
        /// </summary>
        public CombatController BeginCombat()
        {
            RunNodeDefinition node = CurrentNode;
            if (IsRunOver || node == null || node.Kind != RunNodeKind.Combat || node.Enemy == null) return null;

            var player = new PlayerCombatant("The Lampwright", State.MaxLight, State.Light);

            ActiveCombat = new CombatController(player, node.Enemy, State.Deck, config.CombatRules, rng);
            ActiveCombat.CombatEnded += OnCombatEnded;
            ActiveCombat.StartCombat();

            return ActiveCombat;
        }

        private void OnCombatEnded(CombatOutcome outcome)
        {
            if (ActiveCombat == null) return;

            // Light is the one piece of combat state that belongs to the run, so it is copied
            // back at exactly one point rather than kept in sync continuously.
            State.Light = ActiveCombat.State.Player.Light;
            State.Summary.TurnsTaken += ActiveCombat.State.TurnNumber;

            string enemyName = ActiveCombat.State.Enemy.Name;
            int turns = ActiveCombat.State.TurnNumber;

            if (outcome == CombatOutcome.Defeat)
            {
                State.Summary.Record($"Fell to {enemyName} on turn {turns}.");
                EndRun(RunOutcome.Defeat);
                return;
            }

            State.Summary.StagesCleared++;
            State.Summary.Record(
                $"Held off {enemyName} in {turns} {(turns == 1 ? "turn" : "turns")} ({State.Light} Light left).");
        }

        // ---------------------------------------------------------------- card reward nodes

        /// <summary>
        /// The choices for this reward node. Rolled once when the node is entered so that
        /// redrawing the screen cannot reroll the offer.
        /// </summary>
        public IReadOnlyList<CardDefinition> CurrentRewardChoices => currentRewardChoices;

        /// <summary>Adds a drafted card to the run deck and returns the copy that was created.</summary>
        public RuntimeCard TakeReward(CardDefinition definition)
        {
            if (IsRunOver || definition == null) return null;
            if (!currentRewardChoices.Contains(definition)) return null;

            RuntimeCard card = State.CreateCard(definition);
            State.MutableDeck.Add(card);
            State.Summary.CardsAdded++;
            State.Summary.Record($"Took {definition.DisplayName}.");

            currentRewardChoices.Clear();
            return card;
        }

        public void SkipReward()
        {
            if (IsRunOver) return;

            currentRewardChoices.Clear();
            State.Summary.Record("Took nothing.");
        }

        // ---------------------------------------------------------------- shrine nodes

        /// <summary>Upgrades one copy in the deck. Fails if the copy is not ours or cannot be upgraded.</summary>
        public bool UpgradeCard(RuntimeCard card)
        {
            if (!CanUseShrine() || card == null || !State.MutableDeck.Contains(card)) return false;
            if (!card.Upgrade()) return false;

            State.Summary.CardsUpgraded++;
            State.Summary.Record($"Upgraded {card.Title}.");
            ShrineUsed = true;
            return true;
        }

        /// <summary>
        /// Removes a card from the run deck, refusing to shrink the deck below the configured
        /// floor - an empty deck would deadlock a combat where no card can be drawn.
        /// </summary>
        public bool RemoveCard(RuntimeCard card)
        {
            if (!CanUseShrine() || card == null) return false;
            if (State.Deck.Count <= config.MinimumDeckSize) return false;
            if (!State.MutableDeck.Remove(card)) return false;

            State.Summary.CardsRemoved++;
            State.Summary.Record($"Let go of {card.Title}.");
            ShrineUsed = true;
            return true;
        }

        /// <summary>Restores Light at a Shrine. Returns how much was actually restored.</summary>
        public int Mend()
        {
            if (!CanUseShrine()) return 0;

            int before = State.Light;
            State.Light = Math.Min(State.MaxLight, State.Light + config.ShrineMendAmount);

            int restored = State.Light - before;
            State.Summary.Record($"Mended for {restored} Light.");
            ShrineUsed = true;
            return restored;
        }

        public bool CanRemoveCards => State != null && State.Deck.Count > config.MinimumDeckSize;

        private bool CanUseShrine() =>
            !IsRunOver && !ShrineUsed && CurrentNode != null && CurrentNode.Kind == RunNodeKind.Shrine;
    }
}
