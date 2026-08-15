using System;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Run;
using LastLight.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Run
{
    /// <summary>
    /// The shrine: sharpen one card, let go of one card, or rest. Exactly one.
    /// </summary>
    /// <remarks>
    /// This is the node that makes deck progression subtractive as well as additive. Upgrading
    /// changes one copy, not the card - two Ember Strikes can sit in the same deck with only one
    /// of them upgraded - and removal is how a deck stays sharp once drafting has padded it.
    ///
    /// The screen only reads the run and calls its verbs; the "one boon per visit" rule and the
    /// minimum deck size are enforced by the run controller, not here. The buttons reflect those
    /// rules rather than defining them.
    /// </remarks>
    public sealed class ShrineScreen : MonoBehaviour
    {
        private enum Mode
        {
            Choosing = 0,
            Upgrading = 1,
            Removing = 2
        }

        [SerializeField] private CardTray tray;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI promptLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button mendButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private TextMeshProUGUI leaveLabel;

        private RunController run;
        private Action onDone;
        private Mode mode = Mode.Choosing;

        private void Awake()
        {
            if (tray != null) tray.CardClicked += OnCardClicked;
            if (upgradeButton != null) upgradeButton.onClick.AddListener(() => SetMode(Mode.Upgrading));
            if (removeButton != null) removeButton.onClick.AddListener(() => SetMode(Mode.Removing));
            if (mendButton != null) mendButton.onClick.AddListener(Mend);
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
        }

        public void Show(RunController controller, string title, Action done)
        {
            run = controller;
            onDone = done;

            if (titleLabel != null) titleLabel.text = title;
            SetMode(Mode.Choosing);
        }

        private void SetMode(Mode next)
        {
            if (run == null) return;

            mode = next;
            bool spent = run.ShrineUsed;

            if (upgradeButton != null) upgradeButton.interactable = !spent;
            if (removeButton != null) removeButton.interactable = !spent && run.CanRemoveCards;
            if (mendButton != null) mendButton.interactable = !spent;

            if (leaveLabel != null) leaveLabel.text = spent ? "Continue" : "Leave without resting";

            switch (mode)
            {
                case Mode.Upgrading:
                    SetPrompt("Choose a card to sharpen. Only that copy changes.");
                    tray?.Show(run.State.Deck, card => card.CanUpgrade);
                    break;

                case Mode.Removing:
                    SetPrompt($"Choose a card to let go. Your deck cannot drop below " +
                              $"{run.Config.MinimumDeckSize} cards.");
                    tray?.Show(run.State.Deck, _ => run.CanRemoveCards);
                    break;

                default:
                    SetPrompt(spent
                        ? "The shrine is quiet now."
                        : $"Sharpen one card, let go of one card, or rest for " +
                          $"{run.Config.ShrineMendAmount} Light. Only one.");
                    tray?.Clear();
                    break;
            }
        }

        private void SetPrompt(string text)
        {
            if (promptLabel != null) promptLabel.text = text;
        }

        private void OnCardClicked(RuntimeCard card)
        {
            if (run == null || card == null) return;

            bool applied = mode switch
            {
                Mode.Upgrading => run.UpgradeCard(card),
                Mode.Removing => run.RemoveCard(card),
                _ => false
            };

            if (!applied) return;

            SetMode(Mode.Choosing);
            Leave();
        }

        private void Mend()
        {
            if (run == null) return;

            run.Mend();
            SetMode(Mode.Choosing);
            Leave();
        }

        private void Leave()
        {
            tray?.Clear();
            onDone?.Invoke();
        }

#if UNITY_EDITOR
        public void Bind(CardTray cardTray, TextMeshProUGUI title, TextMeshProUGUI prompt,
            Button upgrade, Button remove, Button mend, Button leave, TextMeshProUGUI leaveText)
        {
            tray = cardTray;
            titleLabel = title;
            promptLabel = prompt;
            upgradeButton = upgrade;
            removeButton = remove;
            mendButton = mend;
            leaveButton = leave;
            leaveLabel = leaveText;
        }
#endif
    }
}
