using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Effects;
using LastLight.Gameplay.Enemies;
using NUnit.Framework;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// The phase machine: what happens in what order, when the fight ends, and whether the
    /// enemy's telegraph tells the truth.
    /// </summary>
    [TestFixture]
    public sealed class TurnFlowTests
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

        [Test]
        public void StartCombat_DealsAHandAndHandsControlToThePlayer()
        {
            CombatController combat = data.Combat(data.PassiveEnemy(), data.Copies(strike, 12), handSize: 5);

            combat.StartCombat();

            Assert.AreEqual(CombatPhase.PlayerAction, combat.State.Phase);
            Assert.AreEqual(5, combat.Deck.Hand.Count);
            Assert.AreEqual(3, combat.State.Focus);
            Assert.AreEqual(1, combat.State.TurnNumber);
            Assert.IsTrue(combat.State.IsPlayerInputAllowed);
        }

        [Test]
        public void TheEnemyIntentIsKnownBeforeThePlayerActs()
        {
            EnemyDefinition enemy = data.Enemy("telegraph", 50, data.Attack(7), data.Defend(5));
            CombatController combat = data.Combat(enemy, data.Copies(strike, 10));

            EnemyAction telegraphed = null;
            combat.IntentChanged += action => telegraphed = action;

            combat.StartCombat();

            Assert.IsNotNull(telegraphed, "The first intent must be published before the first player turn.");
            Assert.AreEqual(IntentKind.Attack, telegraphed.Intent);
            Assert.AreEqual(7, combat.PreviewIntentValue());
        }

        [Test]
        public void ThePhasesRunInTheDocumentedOrder()
        {
            CombatController combat = data.Combat(data.PassiveEnemy(), data.Copies(strike, 12));
            var phases = new List<CombatPhase>();
            combat.PhaseChanged += phase => phases.Add(phase);

            combat.StartCombat();
            combat.EndPlayerTurn();

            CollectionAssert.AreEqual(new[]
            {
                CombatPhase.CombatStart,
                CombatPhase.PlayerTurnStart,
                CombatPhase.PlayerAction,
                CombatPhase.PlayerTurnEnd,
                CombatPhase.ResolveCheck,
                CombatPhase.EnemyTurn,
                CombatPhase.ResolveCheck,
                CombatPhase.PlayerTurnStart,
                CombatPhase.PlayerAction
            }, phases);
        }

        [Test]
        public void EndingTheTurn_DiscardsTheHandAndDealsAFreshOne()
        {
            CombatController combat = data.Combat(data.PassiveEnemy(), data.Copies(strike, 12), handSize: 5);
            combat.StartCombat();

            combat.EndPlayerTurn();

            Assert.AreEqual(5, combat.Deck.Hand.Count, "A new hand is dealt for the new turn.");
            Assert.AreEqual(5, combat.Deck.DiscardPile.Count, "The old hand was discarded, not kept.");
            Assert.AreEqual(2, combat.State.TurnNumber);
        }

        [Test]
        public void EndingTheTurn_LetsTheEnemyAct()
        {
            EnemyDefinition enemy = data.Enemy("biter", 50, data.Attack(7));
            CombatController combat = data.Combat(enemy, data.Copies(strike, 12), playerLight: 40);
            combat.StartCombat();

            combat.EndPlayerTurn();

            Assert.AreEqual(33, combat.State.Player.Light);
        }

        [Test]
        public void TheEnemyWalksItsPatternInOrderAndLoops()
        {
            EnemyDefinition enemy = data.Enemy("cycler", 200, data.Attack(3), data.Attack(4), data.Defend(9));
            CombatController combat = data.Combat(enemy, data.Copies(strike, 30), playerLight: 100);
            combat.StartCombat();

            Assert.AreEqual(3, combat.PreviewIntentValue());
            combat.EndPlayerTurn();
            Assert.AreEqual(4, combat.PreviewIntentValue());
            combat.EndPlayerTurn();
            Assert.AreEqual(9, combat.PreviewIntentValue(), "A Defend intent shows the Ward it will gain.");
            combat.EndPlayerTurn();
            Assert.AreEqual(3, combat.PreviewIntentValue(), "The pattern loops back to the start.");
        }

        [Test]
        public void TheTelegraphedNumberIsTheDamageTheEnemyActuallyDeals()
        {
            // Exposed on the player must be reflected in the preview, or the telegraph lies.
            CardDefinition selfExpose = data.Card("expose", 1, CardType.Skill,
                new ApplyStatusEffect(StatusType.Exposed, 2, 2, EffectTargeting.Self));

            EnemyDefinition enemy = data.Enemy("biter", 100, data.Attack(7));
            var deck = new List<RuntimeCard> { new RuntimeCard(1, selfExpose) };

            CombatController combat = data.Combat(enemy, deck, playerLight: 60, handSize: 1);
            combat.StartCombat();
            combat.TryPlayCard(combat.Deck.Hand[0]);

            int predicted = combat.PreviewIntentValue();
            int lightBefore = combat.State.Player.Light;

            combat.EndPlayerTurn();

            Assert.AreEqual(10, predicted, "7 base, x1.5 for Exposed, floored.");
            Assert.AreEqual(lightBefore - predicted, combat.State.Player.Light);
        }

        [Test]
        public void WardSurvivesTheEnemyTurnAndExpiresOnYourNext()
        {
            CardDefinition guard = data.Card("guard", 1, CardType.Skill, new GainWardEffect(10, 14));
            EnemyDefinition enemy = data.Enemy("biter", 100, data.Attack(4));
            var deck = new List<RuntimeCard> { new RuntimeCard(1, guard) };

            CombatController combat = data.Combat(enemy, deck, playerLight: 50, handSize: 1);
            combat.StartCombat();
            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(10, combat.State.Player.Ward);

            combat.EndPlayerTurn();

            Assert.AreEqual(50, combat.State.Player.Light, "Ward should have soaked the whole hit.");
            Assert.AreEqual(0, combat.State.Player.Ward, "Ward is wiped at the start of your next turn.");
        }

        [Test]
        public void ExposedShedsAStackAtTheStartOfItsOwnersTurn()
        {
            CardDefinition selfExpose = data.Card("expose", 1, CardType.Skill,
                new ApplyStatusEffect(StatusType.Exposed, 2, 2, EffectTargeting.Self));
            var deck = new List<RuntimeCard> { new RuntimeCard(1, selfExpose) };

            CombatController combat = data.Combat(data.PassiveEnemy(), deck, handSize: 1);
            combat.StartCombat();
            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(2, combat.State.Player.Statuses.Get(StatusType.Exposed));

            combat.EndPlayerTurn();

            Assert.AreEqual(1, combat.State.Player.Statuses.Get(StatusType.Exposed));
        }

        [Test]
        public void KillingTheEnemyEndsTheCombatInVictory()
        {
            CombatController combat = data.Combat(data.PassiveEnemy(maxLight: 4), data.Copies(strike, 6), handSize: 6);
            CombatOutcome reported = CombatOutcome.InProgress;
            combat.CombatEnded += outcome => reported = outcome;

            combat.StartCombat();
            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(CombatOutcome.Victory, combat.State.Outcome);
            Assert.AreEqual(CombatOutcome.Victory, reported);
            Assert.AreEqual(CombatPhase.CombatEnd, combat.State.Phase);
        }

        [Test]
        public void RunningOutOfLightEndsTheCombatInDefeat()
        {
            EnemyDefinition enemy = data.Enemy("executioner", 100, data.Attack(50));
            CombatController combat = data.Combat(enemy, data.Copies(strike, 12), playerLight: 10);
            CombatOutcome reported = CombatOutcome.InProgress;
            combat.CombatEnded += outcome => reported = outcome;

            combat.StartCombat();
            combat.EndPlayerTurn();

            Assert.AreEqual(CombatOutcome.Defeat, combat.State.Outcome);
            Assert.AreEqual(CombatOutcome.Defeat, reported);
            Assert.IsFalse(combat.State.Player.IsAlive);
        }

        [Test]
        public void NoInputIsAcceptedOnceTheCombatIsOver()
        {
            CombatController combat = data.Combat(data.PassiveEnemy(maxLight: 4), data.Copies(strike, 6), handSize: 6);
            combat.StartCombat();
            combat.TryPlayCard(combat.Deck.Hand[0]);

            int turnAtEnd = combat.State.TurnNumber;
            combat.EndPlayerTurn();

            Assert.IsFalse(combat.State.IsPlayerInputAllowed);
            Assert.AreEqual(turnAtEnd, combat.State.TurnNumber, "Ending a finished combat must do nothing.");
            Assert.AreEqual(CombatPhase.CombatEnd, combat.State.Phase);
        }

        [Test]
        public void EndPlayerTurn_IsIgnoredWhenItIsNotThePlayersTurn()
        {
            CombatController combat = data.Combat(data.PassiveEnemy(), data.Copies(strike, 12));

            combat.EndPlayerTurn(); // never started

            Assert.AreEqual(CombatPhase.NotStarted, combat.State.Phase);
            Assert.AreEqual(0, combat.State.TurnNumber);
        }

        [Test]
        public void StartCombat_IsIgnoredIfCalledTwice()
        {
            CombatController combat = data.Combat(data.PassiveEnemy(), data.Copies(strike, 12), handSize: 5);
            combat.StartCombat();

            combat.StartCombat();

            Assert.AreEqual(1, combat.State.TurnNumber, "A second start must not deal another hand.");
            Assert.AreEqual(5, combat.Deck.Hand.Count);
        }
    }
}
