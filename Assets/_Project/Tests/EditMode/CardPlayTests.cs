using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Effects;
using NUnit.Framework;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// The validation gate. Every one of these is a rejection the UI must be able to explain,
    /// which is why the controller returns a reason rather than a bool.
    /// </summary>
    [TestFixture]
    public sealed class CardPlayTests
    {
        private TestData data;
        private CardDefinition cheapStrike;
        private CardDefinition expensiveStrike;

        [SetUp]
        public void SetUp()
        {
            data = new TestData();
            cheapStrike = data.Card("cheap", 1, new DealDamageEffect(5, 8));
            expensiveStrike = data.Card("expensive", 4, new DealDamageEffect(20, 25));
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        /// <summary>A started combat whose entire deck is in hand, so tests can pick any card.</summary>
        private CombatController StartedCombat(CardDefinition definition, int copies = 6, int enemyLight = 100)
        {
            List<RuntimeCard> deck = data.Copies(definition, copies);
            CombatController combat = data.Combat(data.PassiveEnemy(maxLight: enemyLight), deck, handSize: copies);
            combat.StartCombat();
            return combat;
        }

        [Test]
        public void PlayingACardWithEnoughFocus_Succeeds()
        {
            CombatController combat = StartedCombat(cheapStrike);
            RuntimeCard card = combat.Deck.Hand[0];

            PlayCardResult result = combat.TryPlayCard(card);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PlayRejection.None, result.Rejection);
        }

        [Test]
        public void PlayingACard_SpendsItsCost()
        {
            CombatController combat = StartedCombat(cheapStrike);
            int before = combat.State.Focus;

            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(before - 1, combat.State.Focus);
        }

        [Test]
        public void PlayingACard_MovesItFromHandToDiscard()
        {
            CombatController combat = StartedCombat(cheapStrike);
            RuntimeCard card = combat.Deck.Hand[0];

            combat.TryPlayCard(card);

            CollectionAssert.DoesNotContain(combat.Deck.Hand, card);
            CollectionAssert.Contains(combat.Deck.DiscardPile, card);
        }

        [Test]
        public void PlayingACardYouCannotAfford_IsRejectedWithAReason()
        {
            CombatController combat = StartedCombat(expensiveStrike);
            int focusBefore = combat.State.Focus;
            RuntimeCard card = combat.Deck.Hand[0];

            PlayCardResult result = combat.TryPlayCard(card);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlayRejection.NotEnoughFocus, result.Rejection);
            Assert.AreEqual("Not enough Focus.", result.Message);
            Assert.AreEqual(focusBefore, combat.State.Focus, "A rejected play must not spend Focus.");
            CollectionAssert.Contains(combat.Deck.Hand, card, "A rejected play must not discard the card.");
        }

        [Test]
        public void PlayingACardThatIsNotInHand_IsRejected()
        {
            CombatController combat = StartedCombat(cheapStrike);
            var stranger = new RuntimeCard(9999, cheapStrike);

            PlayCardResult result = combat.TryPlayCard(stranger);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlayRejection.CardNotInHand, result.Rejection);
        }

        [Test]
        public void PlayingNull_IsRejectedRatherThanThrowing()
        {
            CombatController combat = StartedCombat(cheapStrike);

            PlayCardResult result = combat.TryPlayCard(null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlayRejection.CardNotInHand, result.Rejection);
        }

        [Test]
        public void PlayingBeforeCombatStarts_IsRejected()
        {
            List<RuntimeCard> deck = data.Copies(cheapStrike, 5);
            CombatController combat = data.Combat(data.PassiveEnemy(), deck);

            PlayCardResult result = combat.TryPlayCard(deck[0]);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlayRejection.NotPlayerTurn, result.Rejection);
        }

        [Test]
        public void PlayingAfterTheFightIsOver_IsRejected()
        {
            // One-Light enemy: the first strike ends the combat.
            CombatController combat = StartedCombat(cheapStrike, copies: 6, enemyLight: 1);
            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(CombatOutcome.Victory, combat.State.Outcome, "Precondition: the enemy should be dead.");

            PlayCardResult result = combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlayRejection.CombatOver, result.Rejection);
        }

        [Test]
        public void ValidatePlay_AnswersWithoutChangingAnything()
        {
            CombatController combat = StartedCombat(expensiveStrike);
            int focus = combat.State.Focus;
            int handCount = combat.Deck.Hand.Count;

            PlayCardResult result = combat.ValidatePlay(combat.Deck.Hand[0]);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(focus, combat.State.Focus);
            Assert.AreEqual(handCount, combat.Deck.Hand.Count);
        }

        [Test]
        public void RejectionRaisesAnEventSoTheUiCanExplainIt()
        {
            CombatController combat = StartedCombat(expensiveStrike);
            PlayRejection captured = PlayRejection.None;
            combat.CardRejected += result => captured = result.Rejection;

            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(PlayRejection.NotEnoughFocus, captured);
        }

        [Test]
        public void PlayingACard_RaisesCardPlayedOnce()
        {
            CombatController combat = StartedCombat(cheapStrike);
            int played = 0;
            combat.CardPlayed += _ => played++;

            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(1, played);
        }
    }
}
