using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Presentation.Combat;
using UnityEngine;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// Displays a set of cards in a centred grid and reports which one was clicked.
    /// </summary>
    /// <remarks>
    /// Shared by the reward draft and the shrine, which are the same interaction at different
    /// sizes: show some cards, pick one. Reusing the real CardView prefab means a card being
    /// offered looks exactly like the card that ends up in your hand - including generated rules
    /// text and the "+" on an upgraded copy - with no second renderer to keep in sync.
    /// </remarks>
    public sealed class CardTray : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private int columns = 5;
        [SerializeField] private float cardScale = 1f;
        [SerializeField] private Vector2 spacing = new Vector2(24f, 24f);

        private readonly List<CardView> pool = new List<CardView>();

        public event Action<RuntimeCard> CardClicked;

        /// <summary>
        /// Shows the given cards. <paramref name="selectable"/> decides which are lit and
        /// clickable; a null predicate makes everything selectable.
        /// </summary>
        public void Show(IReadOnlyList<RuntimeCard> cards, Func<RuntimeCard, bool> selectable = null)
        {
            int count = cards?.Count ?? 0;
            EnsurePool(count);

            for (int i = 0; i < pool.Count; i++)
            {
                if (i >= count)
                {
                    pool[i].Show(null, false);
                    continue;
                }

                RuntimeCard card = cards[i];
                pool[i].Show(card, selectable == null || selectable(card));
                pool[i].transform.localScale = Vector3.one * cardScale;
            }

            Layout(count);
        }

        public void Clear() => Show(null);

        private void Layout(int count)
        {
            if (count == 0) return;

            float cellWidth = (UiTheme.CardWidth * cardScale) + spacing.x;
            float cellHeight = (UiTheme.CardHeight * cardScale) + spacing.y;

            int rows = Mathf.CeilToInt(count / (float)columns);
            float totalHeight = (rows * cellHeight) - spacing.y;

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;

                // Centre each row independently so a partial last row does not sit off to one side.
                int inThisRow = Mathf.Min(columns, count - (row * columns));
                float rowWidth = (inThisRow * cellWidth) - spacing.x;

                float x = -(rowWidth * 0.5f) + (column * cellWidth) + (UiTheme.CardWidth * cardScale * 0.5f);
                float y = (totalHeight * 0.5f) - (row * cellHeight) - (UiTheme.CardHeight * cardScale * 0.5f);

                pool[i].transform.SetSiblingIndex(i);
                pool[i].SetRestPosition(new Vector2(x, y));
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
            if (view?.Card == null || !view.Playable) return;
            CardClicked?.Invoke(view.Card);
        }

#if UNITY_EDITOR
        public void Bind(RectTransform cardContainer, CardView prefab, int gridColumns, float scale)
        {
            container = cardContainer;
            cardPrefab = prefab;
            columns = gridColumns;
            cardScale = scale;
        }
#endif
    }
}
