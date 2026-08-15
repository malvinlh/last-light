using System;
using System.Text;
using LastLight.Gameplay.Run;
using LastLight.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Run
{
    /// <summary>
    /// The end of a run: how it went, and how to start another.
    /// </summary>
    /// <remarks>
    /// The summary is assembled from <see cref="RunSummary"/>, which the run controller fills in
    /// as things happen rather than reconstructing at the end - by the time the run is over the
    /// combats are gone, so anything not recorded on the way through is unrecoverable.
    /// </remarks>
    public sealed class RunResultScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI subtitleLabel;
        [SerializeField] private TextMeshProUGUI summaryLabel;
        [SerializeField] private TextMeshProUGUI logLabel;
        [SerializeField] private Button newRunButton;
        [SerializeField] private Button menuButton;

        private Action onNewRun;
        private Action onMainMenu;

        private void Awake()
        {
            if (newRunButton != null) newRunButton.onClick.AddListener(() => onNewRun?.Invoke());
            if (menuButton != null) menuButton.onClick.AddListener(() => onMainMenu?.Invoke());
        }

        public void Show(RunState state, RunOutcome outcome, int totalStages, Action newRun, Action mainMenu)
        {
            onNewRun = newRun;
            onMainMenu = mainMenu;

            bool won = outcome == RunOutcome.Victory;

            if (titleLabel != null)
            {
                titleLabel.text = won ? "The Dark Recedes" : "The Light Goes Out";
                titleLabel.color = won ? UiTheme.Light : UiTheme.Danger;
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = won
                    ? "Three nights held. The coast keeps its lighthouse."
                    : "The lantern is cold, and the dark comes inland.";
            }

            if (state == null) return;
            RunSummary summary = state.Summary;

            if (summaryLabel != null)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Stages cleared    {summary.StagesCleared} / {CombatStages(totalStages)}");
                builder.AppendLine($"Turns taken       {summary.TurnsTaken}");
                builder.AppendLine($"Light remaining   {state.Light} / {state.MaxLight}");
                builder.AppendLine($"Cards drafted     {summary.CardsAdded}");
                builder.AppendLine($"Cards sharpened   {summary.CardsUpgraded}");
                builder.AppendLine($"Cards released    {summary.CardsRemoved}");
                builder.Append($"Final deck        {state.Deck.Count} cards");

                summaryLabel.text = builder.ToString();
            }

            if (logLabel != null)
            {
                var builder = new StringBuilder();
                for (int i = 0; i < summary.Log.Count; i++) builder.AppendLine(summary.Log[i]);
                logLabel.text = builder.ToString();
            }
        }

        /// <summary>The summary counts fights, not decision nodes, so the denominator has to match.</summary>
        private static int CombatStages(int totalStages) => totalStages;

#if UNITY_EDITOR
        public void Bind(TextMeshProUGUI title, TextMeshProUGUI subtitle, TextMeshProUGUI summary,
            TextMeshProUGUI log, Button newRun, Button menu)
        {
            titleLabel = title;
            subtitleLabel = subtitle;
            summaryLabel = summary;
            logLabel = log;
            newRunButton = newRun;
            menuButton = menu;
        }
#endif
    }
}
