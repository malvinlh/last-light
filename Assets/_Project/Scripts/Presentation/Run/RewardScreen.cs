using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Run
{
    /// <summary>
    /// The draft between stages: take one of three cards, or take nothing.
    /// </summary>
    /// <remarks>
    /// The offered cards are shown as real <see cref="RuntimeCard"/> instances built from the
    /// offered definitions. They are throwaway previews - the copy that actually joins the deck
    /// is minted by the run controller when the choice is confirmed - but rendering them through
    /// the same view as a card in hand means the preview cannot misrepresent what you are taking.
    ///
    /// Skipping is a real option rather than a courtesy. A deck that grows every stage draws its
    /// good cards less often, so declining is sometimes correct, and the summary records it.
    /// </remarks>
    public sealed class RewardScreen : MonoBehaviour
    {
        [SerializeField] private CardTray tray;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI subtitleLabel;
        [SerializeField] private Button skipButton;

        private readonly List<RuntimeCard> previews = new List<RuntimeCard>();
        private IReadOnlyList<CardDefinition> offered;
        private Action<CardDefinition> onChosen;
        private Action onSkipped;

        private void Awake()
        {
            if (tray != null) tray.CardClicked += OnCardClicked;
            if (skipButton != null) skipButton.onClick.AddListener(() => onSkipped?.Invoke());
        }

        public void Show(string title, string subtitle, IReadOnlyList<CardDefinition> choices,
            Action<CardDefinition> chosen, Action skipped)
        {
            offered = choices;
            onChosen = chosen;
            onSkipped = skipped;

            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;

            previews.Clear();
            for (int i = 0; i < (choices?.Count ?? 0); i++)
            {
                if (choices[i] == null) continue;
                previews.Add(new RuntimeCard(-(i + 1), choices[i]));
            }

            tray?.Show(previews);
        }

        private void OnCardClicked(RuntimeCard preview)
        {
            if (preview == null || offered == null) return;

            // Hand back the definition, not the preview copy - the run owns minting real cards.
            onChosen?.Invoke(preview.Definition);
        }

#if UNITY_EDITOR
        public void Bind(CardTray cardTray, TextMeshProUGUI title, TextMeshProUGUI subtitle, Button skip)
        {
            tray = cardTray;
            titleLabel = title;
            subtitleLabel = subtitle;
            skipButton = skip;
        }
#endif
    }
}
