using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Presentation.Common;
using UnityEngine;

namespace LastLight.Presentation.Combat
{
    /// <summary>
    /// Lays the hand out and keeps a pool of card views.
    /// </summary>
    /// <remarks>
    /// Views are pooled and re-shown rather than destroyed and re-instantiated each time the
    /// hand changes - a hand changes several times per turn, and recreating TMP objects that
    /// often causes a visible hitch.
    ///
    /// Positioning is done here instead of with a layout group because cards need to overlap
    /// once the hand gets wide, which a HorizontalLayoutGroup cannot express.
    /// </remarks>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private float maxWidth = 1360f;

        private readonly List<CardView> pool = new List<CardView>();

        /// <summary>Raised when a card in hand is clicked.</summary>
        public event Action<RuntimeCard> CardClicked;

        /// <summary>
        /// Rebuilds the hand display. <paramref name="canPlay"/> is asked per card so the view
        /// greys out exactly what the controller would refuse.
        /// </summary>
        public void Show(IReadOnlyList<RuntimeCard> hand, Func<RuntimeCard, bool> canPlay)
        {
            int count = hand?.Count ?? 0;
            EnsurePool(count);

            for (int i = 0; i < pool.Count; i++)
            {
                if (i >= count)
                {
                    pool[i].Show(null, false);
                    continue;
                }

                RuntimeCard card = hand[i];
                pool[i].Show(card, canPlay == null || canPlay(card));
            }

            Layout(count);
        }

        private void Layout(int count)
        {
            if (count == 0) return;

            float step = UiTheme.CardWidth + UiTheme.CardSpacing;
            float totalWidth = (count - 1) * step;

            // Once the hand is wider than the tray, tighten the spacing so cards overlap
            // instead of running off the edges.
            if (totalWidth > maxWidth && count > 1)
            {
                step = maxWidth / (count - 1);
                totalWidth = maxWidth;
            }

            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                pool[i].transform.SetSiblingIndex(i);
                pool[i].SetRestPosition(new Vector2(startX + (step * i), 0f));
            }
        }

        private void EnsurePool(int required)
        {
            while (pool.Count < required)
            {
                CardView view = Instantiate(cardPrefab, container);
                view.Clicked += OnCardClicked;
                pool.Add(view);
            }
        }

        private void OnCardClicked(CardView view)
        {
            if (view?.Card == null) return;
            CardClicked?.Invoke(view.Card);
        }

#if UNITY_EDITOR
        public void Bind(RectTransform cardContainer, CardView prefab)
        {
            container = cardContainer;
            cardPrefab = prefab;
        }
#endif
    }
}
