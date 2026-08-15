using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Common;

namespace LastLight.Gameplay.Deck
{
    /// <summary>
    /// The three piles of one combat: draw, hand, discard.
    /// </summary>
    /// <remarks>
    /// A fresh DeckService is built for each combat over the run's persistent card list, so
    /// the piles are per-combat state while card ownership and upgrades are per-run state.
    /// It owns no rules about *why* cards move - the combat layer decides that - which keeps
    /// this class small enough to test exhaustively.
    /// </remarks>
    public sealed class DeckService
    {
        private readonly List<RuntimeCard> drawPile = new List<RuntimeCard>();
        private readonly List<RuntimeCard> hand = new List<RuntimeCard>();
        private readonly List<RuntimeCard> discardPile = new List<RuntimeCard>();
        private readonly GameRng rng;

        public DeckService(IEnumerable<RuntimeCard> cards, GameRng rng)
        {
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));

            if (cards != null) drawPile.AddRange(cards);
            rng.Shuffle(drawPile);
        }

        public IReadOnlyList<RuntimeCard> DrawPile => drawPile;
        public IReadOnlyList<RuntimeCard> Hand => hand;
        public IReadOnlyList<RuntimeCard> DiscardPile => discardPile;

        /// <summary>Total cards still in the combat, across all three piles.</summary>
        public int TotalCards => drawPile.Count + hand.Count + discardPile.Count;

        /// <summary>Raised whenever any pile changes, so views can re-render without polling.</summary>
        public event Action Changed;

        /// <summary>
        /// Draws up to <paramref name="count"/> cards and returns how many were actually drawn.
        /// Reshuffles the discard pile when the draw pile runs dry, and stops early (rather than
        /// looping forever) if every pile is empty - a real possibility once cards are removed
        /// at a Shrine.
        /// </summary>
        public int Draw(int count)
        {
            int drawn = 0;

            for (int i = 0; i < count; i++)
            {
                if (drawPile.Count == 0) ReshuffleDiscardIntoDrawPile();
                if (drawPile.Count == 0) break;

                int top = drawPile.Count - 1;
                hand.Add(drawPile[top]);
                drawPile.RemoveAt(top);
                drawn++;
            }

            if (drawn > 0) Changed?.Invoke();
            return drawn;
        }

        public bool IsInHand(RuntimeCard card) => card != null && hand.Contains(card);

        public bool RemoveFromHand(RuntimeCard card)
        {
            if (card == null || !hand.Remove(card)) return false;
            Changed?.Invoke();
            return true;
        }

        public void AddToDiscard(RuntimeCard card)
        {
            if (card == null) return;
            discardPile.Add(card);
            Changed?.Invoke();
        }

        public void DiscardHand()
        {
            if (hand.Count == 0) return;
            discardPile.AddRange(hand);
            hand.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// Moves the discard pile back under the draw pile and shuffles it. The draw pile is
        /// expected to be empty when this happens; any remaining cards are kept rather than
        /// dropped so no card can ever leave the combat by accident.
        /// </summary>
        public void ReshuffleDiscardIntoDrawPile()
        {
            if (discardPile.Count == 0) return;

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            rng.Shuffle(drawPile);
            Changed?.Invoke();
        }
    }
}
