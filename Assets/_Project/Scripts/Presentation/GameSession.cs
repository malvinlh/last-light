using System;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Common;
using LastLight.Gameplay.Run;
using LastLight.Presentation.Combat;
using LastLight.Presentation.Common;
using UnityEngine;

namespace LastLight.Presentation
{
    /// <summary>
    /// The one MonoBehaviour that owns the run and decides what happens next.
    /// </summary>
    /// <remarks>
    /// Everything stateful lives in plain C# below this line - RunController owns the run,
    /// CombatController owns the fight. This class exists only to give them a lifetime tied to
    /// the scene, to feed player input in, and to route between screens.
    ///
    /// Scope note: at this milestone the session plays the run's first combat and offers a
    /// restart. Node routing (rewards, shrine, the later stages) is the next milestone, which
    /// is why the run controller is already the owner here rather than a bare combat.
    /// </remarks>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private RunConfig runConfig;
        [SerializeField] private CombatScreen combatScreen;

        [SerializeField, Tooltip("Use a fixed seed so a bug can be reproduced exactly.")]
        private bool useFixedSeed;

        [SerializeField] private int fixedSeed = 20260815;

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

        // ---------------------------------------------------------------- flow

        /// <summary>
        /// Throws away any previous run and starts a fresh one. The run controller rebuilds its
        /// own state, so nothing here needs to remember what to clear.
        /// </summary>
        public void StartNewRun()
        {
            int seed = useFixedSeed ? fixedSeed : Environment.TickCount;

            Run = new RunController(runConfig, new GameRng(seed));
            Run.StartNewRun();

            BeginCurrentCombat();
        }

        private void BeginCurrentCombat()
        {
            CombatController controller = Run.BeginCombat();

            if (controller == null)
            {
                Debug.LogError("[LastLight] The current run node is not a combat.");
                return;
            }

            controller.CombatEnded += OnCombatEnded;
            combatScreen?.Bind(controller, StageText());
        }

        private string StageText()
        {
            RunNodeDefinition node = Run?.CurrentNode;
            if (node == null) return string.Empty;

            return $"Stage {Run.State.NodeIndex + 1} of {Run.NodeCount}  -  {node.Title}";
        }

        private void OnCombatEnded(CombatOutcome outcome)
        {
            if (combatScreen == null) return;

            if (outcome == CombatOutcome.Victory)
            {
                combatScreen.ShowOutcome(
                    "The watch holds",
                    UiTheme.Light,
                    $"{Run.State.Light} Light still burning after {Combat.State.TurnNumber} turns.",
                    "New Run",
                    StartNewRun);
                return;
            }

            combatScreen.ShowOutcome(
                "The light goes out",
                UiTheme.Danger,
                $"The dark took the lantern on turn {Combat.State.TurnNumber}.",
                "New Run",
                StartNewRun);
        }

        // ---------------------------------------------------------------- player input

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
        public void Bind(RunConfig config, CombatScreen screen)
        {
            runConfig = config;
            combatScreen = screen;
        }
#endif
    }
}
