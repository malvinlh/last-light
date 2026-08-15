using System;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Common;
using LastLight.Gameplay.Run;
using LastLight.Presentation.Combat;
using LastLight.Presentation.Common;
using LastLight.Presentation.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.Presentation
{
    /// <summary>
    /// The one MonoBehaviour that owns the run and decides what happens next.
    /// </summary>
    /// <remarks>
    /// Everything stateful lives in plain C# below this line - RunController owns the run,
    /// CombatController owns the fight. This class gives them a lifetime tied to the scene,
    /// feeds player input in, and routes between screens.
    ///
    /// Routing is driven by the run controller's own NodeEntered event rather than by this class
    /// walking the node list. That means there is exactly one place that decides where you are -
    /// the run - and advancing is always the same call no matter which screen you came from.
    ///
    /// The two ways a run can end both funnel through RunEnded: losing a fight ends it from
    /// inside combat, and clearing the final node ends it from the advance. Neither path needs a
    /// special case here.
    /// </remarks>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private RunConfig runConfig;
        [SerializeField] private ScreenRouter router;
        [SerializeField] private CombatScreen combatScreen;
        [SerializeField] private RewardScreen rewardScreen;
        [SerializeField] private ShrineScreen shrineScreen;
        [SerializeField] private RunResultScreen runResultScreen;

        [SerializeField, Tooltip("Use a fixed seed so a bug can be reproduced exactly.")]
        private bool useFixedSeed;

        [SerializeField] private int fixedSeed = 20260815;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        public RunController Run { get; private set; }

        public CombatController Combat => Run?.ActiveCombat;

        private void Awake()
        {
            if (combatScreen != null) combatScreen.Initialize(this);
        }

        private void Start()
        {
            if (runConfig == null)
            {
                Debug.LogError("[LastLight] GameSession has no RunConfig assigned; nothing to play.");
                return;
            }

            StartNewRun();
        }

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>
        /// Throws away any previous run and starts a fresh one, in place - no scene reload. The
        /// run controller rebuilds its own state, so nothing here needs to remember what to clear.
        /// A new seed each time means a new run is genuinely a new run.
        /// </summary>
        public void StartNewRun()
        {
            DetachRun();

            int seed = useFixedSeed ? fixedSeed : Environment.TickCount;

            Run = new RunController(runConfig, new GameRng(seed));
            Run.NodeEntered += OnNodeEntered;
            Run.RunEnded += OnRunEnded;

            Run.StartNewRun();
        }

        private void DetachRun()
        {
            if (Run == null) return;

            Run.NodeEntered -= OnNodeEntered;
            Run.RunEnded -= OnRunEnded;
        }

        private void OnDestroy() => DetachRun();

        public void GoToMainMenu() => SceneManager.LoadScene(mainMenuSceneName);

        // ---------------------------------------------------------------- node routing

        private void OnNodeEntered(RunNodeDefinition node)
        {
            if (node == null) return;

            switch (node.Kind)
            {
                case RunNodeKind.Combat:
                    EnterCombat(node);
                    break;

                case RunNodeKind.CardReward:
                    EnterReward(node);
                    break;

                case RunNodeKind.Shrine:
                    EnterShrine(node);
                    break;

                default:
                    Debug.LogError($"[LastLight] Unhandled run node kind '{node.Kind}'.");
                    break;
            }
        }

        private void EnterCombat(RunNodeDefinition node)
        {
            CombatController controller = Run.BeginCombat();

            if (controller == null)
            {
                Debug.LogError($"[LastLight] Node '{node.Title}' is a combat but produced no controller.");
                return;
            }

            controller.CombatEnded += OnCombatEnded;
            combatScreen?.Bind(controller, StageText(node));
            router?.ShowCombat();
        }

        private void EnterReward(RunNodeDefinition node)
        {
            rewardScreen?.Show(node.Title, node.Subtitle, Run.CurrentRewardChoices,
                OnRewardChosen, OnRewardSkipped);
            router?.ShowReward();
        }

        private void EnterShrine(RunNodeDefinition node)
        {
            shrineScreen?.Show(Run, node.Title, Advance);
            router?.ShowShrine();
        }

        /// <summary>Moves to the next node. Running off the end of the list is how a run is won.</summary>
        public void Advance() => Run?.AdvanceToNextNode();

        private string StageText(RunNodeDefinition node) =>
            $"Stage {Run.State.NodeIndex + 1} of {Run.NodeCount}  -  {node.Title}";

        // ---------------------------------------------------------------- outcomes

        private void OnCombatEnded(CombatOutcome outcome)
        {
            // A defeat ends the whole run, which arrives through OnRunEnded instead.
            if (outcome != CombatOutcome.Victory) return;

            combatScreen?.ShowOutcome(
                "The watch holds",
                UiTheme.Light,
                $"{Run.State.Light} Light still burning after {Combat.State.TurnNumber} turns.",
                "Continue",
                Advance);
        }

        private void OnRunEnded(RunOutcome outcome)
        {
            runResultScreen?.Show(Run.State, outcome, CombatNodeCount(), StartNewRun, GoToMainMenu);
            router?.ShowRunResult();
        }

        private int CombatNodeCount()
        {
            int count = 0;
            for (int i = 0; i < runConfig.Nodes.Count; i++)
            {
                if (runConfig.Nodes[i].Kind == RunNodeKind.Combat) count++;
            }

            return count;
        }

        // ---------------------------------------------------------------- player input

        private void OnRewardChosen(CardDefinition definition)
        {
            Run.TakeReward(definition);
            Advance();
        }

        private void OnRewardSkipped()
        {
            Run.SkipReward();
            Advance();
        }

        /// <summary>
        /// The only route from a click to the rules. Returns the result so callers can react,
        /// though the controller also raises a rejection event that drives the on-screen toast.
        /// </summary>
        public PlayCardResult TryPlayCard(RuntimeCard card)
        {
            if (Combat == null) return PlayCardResult.Rejected(PlayRejection.CombatOver);
            return Combat.TryPlayCard(card);
        }

        public void EndTurn() => Combat?.EndPlayerTurn();

#if UNITY_EDITOR
        public void Bind(RunConfig config, ScreenRouter screenRouter, CombatScreen combat,
            RewardScreen reward, ShrineScreen shrine, RunResultScreen result, string menuScene)
        {
            runConfig = config;
            router = screenRouter;
            combatScreen = combat;
            rewardScreen = reward;
            shrineScreen = shrine;
            runResultScreen = result;
            mainMenuSceneName = menuScene;
        }
#endif
    }
}
