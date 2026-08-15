using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Common;
using LastLight.Gameplay.Deck;
using LastLight.Gameplay.Effects;
using NUnit.Framework;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// The pile mechanics: draw, discard, reshuffle, and the edge cases that would otherwise
    /// surface as a hang or a vanished card halfway through a run.
    /// </summary>
    [TestFixture]
    public sealed class DeckServiceTests
    {
        private TestData data;
        private CardDefinition strike;

        [SetUp]
        public void SetUp()
        {
            data = new TestData();
            strike = data.Card("strike", 1, new DealDamageEffect(5, 8));
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        private DeckService Deck(int cardCount, int seed = 1) =>
            new DeckService(data.Copies(strike, cardCount), new GameRng(seed));

        [Test]
        public void Constructor_PutsEveryCardInTheDrawPile()
        {
            DeckService deck = Deck(10);

            Assert.AreEqual(10, deck.DrawPile.Count);
            Assert.AreEqual(0, deck.Hand.Count);
            Assert.AreEqual(0, deck.DiscardPile.Count);
        }

        [Test]
        public void Shuffle_KeepsEveryCard()
        {
            List<RuntimeCard> cards = data.Copies(strike, 20);
            var deck = new DeckService(cards, new GameRng(12345));

            var ids = new HashSet<int>();
            foreach (RuntimeCard card in deck.DrawPile) ids.Add(card.InstanceId);

            Assert.AreEqual(20, ids.Count, "Shuffling must not duplicate or drop cards.");
        }

        [Test]
        public void Shuffle_IsDeterministicForASeed()
        {
            var first = new DeckService(data.Copies(strike, 20), new GameRng(99));
            var second = new DeckService(data.Copies(strike, 20), new GameRng(99));

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(first.DrawPile[i].InstanceId, second.DrawPile[i].InstanceId,
                    "The same seed must produce the same order, or nothing here is reproducible.");
            }
        }

        [Test]
        public void Draw_MovesCardsFromDrawPileToHand()
        {
            DeckService deck = Deck(10);

            int drawn = deck.Draw(5);

            Assert.AreEqual(5, drawn);
            Assert.AreEqual(5, deck.Hand.Count);
            Assert.AreEqual(5, deck.DrawPile.Count);
        }

        [Test]
        public void DiscardHand_MovesTheWholeHandToTheDiscardPile()
        {
            DeckService deck = Deck(10);
            deck.Draw(5);

            deck.DiscardHand();

            Assert.AreEqual(0, deck.Hand.Count);
            Assert.AreEqual(5, deck.DiscardPile.Count);
        }

        [Test]
        public void Draw_ReshufflesTheDiscardPileWhenTheDrawPileRunsOut()
        {
            DeckService deck = Deck(5);

            deck.Draw(5);        // whole deck in hand, draw pile empty
            deck.DiscardHand();  // all five now in discard

            Assert.AreEqual(0, deck.DrawPile.Count, "Precondition: the draw pile should be empty.");

            int drawn = deck.Draw(3);

            Assert.AreEqual(3, drawn, "Drawing must recycle the discard pile rather than fail.");
            Assert.AreEqual(3, deck.Hand.Count);
            Assert.AreEqual(2, deck.DrawPile.Count);
            Assert.AreEqual(0, deck.DiscardPile.Count);
        }

        [Test]
        public void Draw_StopsWhenEveryPileIsEmpty()
        {
            DeckService deck = Deck(3);

            int drawn = deck.Draw(10);

            Assert.AreEqual(3, drawn, "Draw must report what it actually drew, not what was asked for.");
            Assert.AreEqual(3, deck.Hand.Count);
        }

        [Test]
        public void Draw_OnACompletelyEmptyDeckIsSafe()
        {
            var deck = new DeckService(new List<RuntimeCard>(), new GameRng(1));

            int drawn = deck.Draw(5);

            Assert.AreEqual(0, drawn, "An empty deck must return zero rather than loop forever.");
            Assert.AreEqual(0, deck.Hand.Count);
        }

        [Test]
        public void CardsAreNeverLostAcrossAFullCycle()
        {
            DeckService deck = Deck(12);

            for (int turn = 0; turn < 6; turn++)
            {
                deck.Draw(5);
                deck.DiscardHand();
                Assert.AreEqual(12, deck.TotalCards, $"A card went missing on turn {turn + 1}.");
            }
        }

        [Test]
        public void RemoveFromHand_OnlySucceedsForCardsActuallyHeld()
        {
            DeckService deck = Deck(5);
            deck.Draw(2);

            RuntimeCard held = deck.Hand[0];
            var stranger = new RuntimeCard(999, strike);

            Assert.IsTrue(deck.RemoveFromHand(held));
            Assert.IsFalse(deck.RemoveFromHand(stranger), "A card that was never in hand must not be removable.");
            Assert.IsFalse(deck.RemoveFromHand(null));
        }

        [Test]
        public void Changed_FiresWhenPilesMove()
        {
            DeckService deck = Deck(5);
            int events = 0;
            deck.Changed += () => events++;

            deck.Draw(2);
            deck.DiscardHand();

            Assert.AreEqual(2, events);
        }
    }
}
