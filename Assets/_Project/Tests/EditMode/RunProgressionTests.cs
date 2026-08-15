using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Common;
using LastLight.Gameplay.Effects;
using LastLight.Gameplay.Enemies;
using LastLight.Gameplay.Run;
using NUnit.Framework;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// The run layer: carrying Light and cards between stages, the two decision node types,
    /// and the reset that a New Run has to perform.
    /// </summary>
    [TestFixture]
    public sealed class RunProgressionTests
    {
        private TestData data;
        private CardDefinition strike;
        private CardDefinition guard;
        private CardDefinition boonA;
        private CardDefinition boonB;
        private CardDefinition boonC;

        [SetUp]
        public void SetUp()
        {
            data = new TestData();
            strike = data.Card("strike", 1, new DealDamageEffect(6, 9));
            guard = data.Card("guard", 1, CardType.Skill, new GainWardEffect(5, 8));
            boonA = data.Card("boon_a", 1, new DealDamageEffect(10, 14));
            boonB = data.Card("boon_b", 1, CardType.Skill, new GainWardEffect(10, 14));
            boonC = data.Card("boon_c", 1, CardType.Skill, new HealEffect(10, 14));
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        private List<StarterDeckEntry> Starter() => new List<StarterDeckEntry>
        {
            new StarterDeckEntry(strike, 5),
            new StarterDeckEntry(guard, 5)
        };

        /// <summary>
        /// A deck of nothing but attacks, for tests that need a combat to end on a known turn.
        /// The mixed starter deck can deal a hand of pure defence, which would make the number
        /// of turns - and therefore the damage taken - depend on the shuffle.
        /// </summary>
        private List<StarterDeckEntry> AllAttackStarter() => new List<StarterDeckEntry>
        {
            new StarterDeckEntry(strike, 10)
        };

        private List<CardDefinition> RewardPool() => new List<CardDefinition> { boonA, boonB, boonC };

        /// <summary>An enemy that dies to a single strike, so combats in these tests are one click.</summary>
        private EnemyDefinition Pushover(string id) =>
            data.Enemy(id, 1, new EnemyAction("Wait", IntentKind.Defend, new CardEffect[0]));

        private RunController Run(List<RunNodeDefinition> nodes, int light = 50, int minDeckSize = 5,
            List<StarterDeckEntry> starter = null)
        {
            RunConfig config = data.RunConfig(starter ?? Starter(), RewardPool(), nodes,
                light: light, minDeckSize: minDeckSize);
            var controller = new RunController(config, new GameRng(7));
            controller.StartNewRun();
            return controller;
        }

        /// <summary>Plays an in-progress combat to a win the way a player would: cards first, then end turn.</summary>
        private static void PlayToVictory(CombatController combat)
        {
            int safety = 0;
            while (combat.State.Outcome == CombatOutcome.InProgress && safety++ < 200)
            {
                bool playedSomething = false;

                foreach (RuntimeCard card in new List<RuntimeCard>(combat.Deck.Hand))
                {
                    if (combat.TryPlayCard(card).Success)
                    {
                        playedSomething = true;
                        break;
                    }
                }

                if (!playedSomething) combat.EndPlayerTurn();
            }

            Assert.AreEqual(CombatOutcome.Victory, combat.State.Outcome, "The test combat should be winnable.");
        }

        /// <summary>Begins the current combat node and plays it to a win.</summary>
        private static void WinCurrentCombat(RunController run)
        {
            CombatController combat = run.BeginCombat();
            Assert.IsNotNull(combat, "Expected the current node to be a combat.");
            PlayToVictory(combat);
        }

        // ---------------------------------------------------------------- starting a run

        [Test]
        public void StartNewRun_BuildsTheStarterDeckAtFullLight()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "", Pushover("a"))
            });

            Assert.AreEqual(10, run.State.Deck.Count);
            Assert.AreEqual(50, run.State.Light);
            Assert.AreEqual(50, run.State.MaxLight);
            Assert.AreEqual(0, run.State.NodeIndex);
            Assert.AreEqual(RunOutcome.InProgress, run.State.Outcome);
        }

        [Test]
        public void EveryCardInTheStarterDeckIsADistinctCopy()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "", Pushover("a"))
            });

            var ids = new HashSet<int>();
            foreach (RuntimeCard card in run.State.Deck) ids.Add(card.InstanceId);

            Assert.AreEqual(10, ids.Count, "Five copies of a card must be five objects, not one shared object.");
        }

        // ---------------------------------------------------------------- moving between stages

        [Test]
        public void LightCarriesFromOneStageToTheNext()
        {
            var nodes = new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "",
                    data.Enemy("biter", 1, data.Attack(9))),
                new RunNodeDefinition(RunNodeKind.Combat, "Two", "", Pushover("b"))
            };

            RunController run = Run(nodes, starter: AllAttackStarter());

            // Pass the first turn so the enemy lands a hit, then finish the fight.
            CombatController combat = run.BeginCombat();
            combat.EndPlayerTurn();
            PlayToVictory(combat);

            int lightAtEnd = combat.State.Player.Light;

            Assert.Less(lightAtEnd, 50, "Precondition: the enemy should have landed a hit.");
            Assert.AreEqual(lightAtEnd, run.State.Light, "Run Light must follow the combat, not reset.");

            run.AdvanceToNextNode();
            CombatController second = run.BeginCombat();

            Assert.AreEqual(lightAtEnd, second.State.Player.Light, "Stage 2 must start on the Light you had left.");
        }

        [Test]
        public void ClearingTheLastNodeWinsTheRun()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "Only", "", Pushover("a"))
            });

            RunOutcome reported = RunOutcome.InProgress;
            run.RunEnded += outcome => reported = outcome;

            WinCurrentCombat(run);
            run.AdvanceToNextNode();

            Assert.AreEqual(RunOutcome.Victory, run.State.Outcome);
            Assert.AreEqual(RunOutcome.Victory, reported);
            Assert.IsTrue(run.IsRunOver);
        }

        [Test]
        public void DyingEndsTheRunImmediately()
        {
            var nodes = new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "",
                    data.Enemy("executioner", 500, data.Attack(50))),
                new RunNodeDefinition(RunNodeKind.Combat, "Two", "", Pushover("b"))
            };

            RunController run = Run(nodes, light: 20);

            CombatController combat = run.BeginCombat();
            combat.EndPlayerTurn();

            Assert.AreEqual(CombatOutcome.Defeat, combat.State.Outcome);
            Assert.AreEqual(RunOutcome.Defeat, run.State.Outcome);
            Assert.IsTrue(run.IsRunOver);

            run.AdvanceToNextNode();
            Assert.AreEqual(0, run.State.NodeIndex, "A finished run must not advance.");
        }

        // ---------------------------------------------------------------- card reward nodes

        [Test]
        public void ARewardNodeOffersDistinctChoices()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage", "")
            });

            IReadOnlyList<CardDefinition> choices = run.CurrentRewardChoices;

            Assert.AreEqual(3, choices.Count);
            CollectionAssert.AllItemsAreUnique(choices);
        }

        [Test]
        public void RewardChoicesDoNotRerollWhileYouLookAtThem()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage", "")
            });

            var first = new List<CardDefinition>(run.CurrentRewardChoices);
            var second = new List<CardDefinition>(run.CurrentRewardChoices);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void TakingARewardPutsThatExactCardIntoTheRunDeck()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage", "")
            });

            CardDefinition chosen = run.CurrentRewardChoices[0];
            RuntimeCard added = run.TakeReward(chosen);

            Assert.IsNotNull(added);
            Assert.AreEqual(11, run.State.Deck.Count);
            CollectionAssert.Contains(run.State.Deck, added);
            Assert.AreEqual(chosen, added.Definition);
            Assert.AreEqual(1, run.State.Summary.CardsAdded);
        }

        [Test]
        public void ADraftedCardIsInTheDeckTheNextStageIsPlayedWith()
        {
            var nodes = new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage", ""),
                new RunNodeDefinition(RunNodeKind.Combat, "Two", "", Pushover("b"))
            };

            RunController run = Run(nodes);
            CardDefinition chosen = run.CurrentRewardChoices[0];
            run.TakeReward(chosen);
            run.AdvanceToNextNode();

            CombatController combat = run.BeginCombat();

            Assert.AreEqual(11, combat.Deck.TotalCards, "The new card must be shuffled into the next fight.");

            bool found = false;
            foreach (RuntimeCard card in combat.Deck.DrawPile) found |= card.Definition == chosen;
            foreach (RuntimeCard card in combat.Deck.Hand) found |= card.Definition == chosen;

            Assert.IsTrue(found, "The drafted card should be somewhere in the next combat's piles.");
        }

        [Test]
        public void SkippingARewardChangesNothing()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage", "")
            });

            run.SkipReward();

            Assert.AreEqual(10, run.State.Deck.Count);
            Assert.AreEqual(0, run.State.Summary.CardsAdded);
        }

        [Test]
        public void ACardThatWasNotOfferedCannotBeTaken()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage", "")
            });

            RuntimeCard sneaked = run.TakeReward(strike);

            Assert.IsNull(sneaked);
            Assert.AreEqual(10, run.State.Deck.Count);
        }

        // ---------------------------------------------------------------- shrine nodes

        private RunController ShrineRun(int minDeckSize = 5) => Run(new List<RunNodeDefinition>
        {
            new RunNodeDefinition(RunNodeKind.Shrine, "Shrine", "")
        }, minDeckSize: minDeckSize);

        [Test]
        public void AShrineCanUpgradeOneCopyWithoutTouchingTheOthers()
        {
            RunController run = ShrineRun();
            RuntimeCard target = run.State.Deck[0];
            CardDefinition definition = target.Definition;

            Assert.IsTrue(run.UpgradeCard(target));

            Assert.IsTrue(target.IsUpgraded);
            Assert.AreEqual(1, run.State.Summary.CardsUpgraded);

            int upgradedCopies = 0;
            foreach (RuntimeCard card in run.State.Deck)
            {
                if (card.Definition == definition && card.IsUpgraded) upgradedCopies++;
            }

            Assert.AreEqual(1, upgradedCopies, "Only the chosen copy should change.");
        }

        [Test]
        public void AShrineCanRemoveACard()
        {
            RunController run = ShrineRun();
            RuntimeCard target = run.State.Deck[0];

            Assert.IsTrue(run.RemoveCard(target));

            Assert.AreEqual(9, run.State.Deck.Count);
            CollectionAssert.DoesNotContain(run.State.Deck, target);
            Assert.AreEqual(1, run.State.Summary.CardsRemoved);
        }

        [Test]
        public void AShrineWillNotShrinkTheDeckBelowTheFloor()
        {
            RunController run = ShrineRun(minDeckSize: 10);

            bool removed = run.RemoveCard(run.State.Deck[0]);

            Assert.IsFalse(removed, "Removing here would leave a deck too small to draw from.");
            Assert.AreEqual(10, run.State.Deck.Count);
            Assert.IsFalse(run.CanRemoveCards);
        }

        [Test]
        public void AShrineGrantsExactlyOneBoon()
        {
            RunController run = ShrineRun();

            Assert.IsTrue(run.UpgradeCard(run.State.Deck[0]));
            Assert.IsFalse(run.UpgradeCard(run.State.Deck[1]), "The shrine is spent.");
            Assert.IsFalse(run.RemoveCard(run.State.Deck[1]));
            Assert.AreEqual(0, run.Mend());
        }

        [Test]
        public void MendingAtFullLightRestoresNothing()
        {
            RunController run = ShrineRun();

            int restored = run.Mend();

            Assert.AreEqual(0, restored, "Mend is clamped to the maximum, so a healthy player gains nothing.");
            Assert.AreEqual(50, run.State.Light);
        }

        [Test]
        public void MendingAfterTakingDamageRestoresLight()
        {
            var nodes = new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "",
                    data.Enemy("biter", 1, data.Attack(20))),
                new RunNodeDefinition(RunNodeKind.Shrine, "Shrine", "")
            };

            RunController run = Run(nodes, starter: AllAttackStarter());

            CombatController combat = run.BeginCombat();
            combat.EndPlayerTurn();
            PlayToVictory(combat);

            int damaged = run.State.Light;
            Assert.Less(damaged, 50, "Precondition: the player should be hurt.");

            run.AdvanceToNextNode();
            int restored = run.Mend();

            Assert.AreEqual(12, restored);
            Assert.AreEqual(damaged + 12, run.State.Light);
        }

        [Test]
        public void ShrineVerbsDoNothingOnANonShrineNode()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "", Pushover("a"))
            });

            Assert.IsFalse(run.UpgradeCard(run.State.Deck[0]));
            Assert.IsFalse(run.RemoveCard(run.State.Deck[0]));
            Assert.AreEqual(0, run.Mend());
        }

        // ---------------------------------------------------------------- new run

        [Test]
        public void StartingANewRunWipesEverythingFromThePreviousOne()
        {
            var nodes = new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "", Pushover("a")),
                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage", ""),
                new RunNodeDefinition(RunNodeKind.Shrine, "Shrine", "")
            };

            RunController run = Run(nodes);

            WinCurrentCombat(run);
            run.AdvanceToNextNode();
            run.TakeReward(run.CurrentRewardChoices[0]);
            run.AdvanceToNextNode();
            run.UpgradeCard(run.State.Deck[0]);

            Assert.AreEqual(11, run.State.Deck.Count, "Precondition: the run should have changed.");

            run.StartNewRun();

            Assert.AreEqual(10, run.State.Deck.Count, "The deck is back to the starter list.");
            Assert.AreEqual(50, run.State.Light);
            Assert.AreEqual(0, run.State.NodeIndex);
            Assert.AreEqual(RunOutcome.InProgress, run.State.Outcome);
            Assert.IsNull(run.ActiveCombat);
            Assert.IsFalse(run.ShrineUsed);

            Assert.AreEqual(0, run.State.Summary.StagesCleared);
            Assert.AreEqual(0, run.State.Summary.CardsAdded);
            Assert.AreEqual(0, run.State.Summary.CardsUpgraded);
            Assert.AreEqual(0, run.State.Summary.Log.Count);

            foreach (RuntimeCard card in run.State.Deck)
            {
                Assert.IsFalse(card.IsUpgraded, "No upgrade may survive into a new run.");
            }
        }

        [Test]
        public void CardInstanceIdsRestartWithEachRun()
        {
            RunController run = Run(new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "", Pushover("a"))
            });

            int firstId = run.State.Deck[0].InstanceId;
            run.StartNewRun();

            Assert.AreEqual(firstId, run.State.Deck[0].InstanceId,
                "Ids come from the run, not a static counter, so a new run starts from the same place.");
        }

        [Test]
        public void TheSummaryRecordsWhatHappened()
        {
            var nodes = new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "One", "", Pushover("a"))
            };

            RunController run = Run(nodes);
            WinCurrentCombat(run);
            run.AdvanceToNextNode();

            Assert.AreEqual(1, run.State.Summary.StagesCleared);
            Assert.GreaterOrEqual(run.State.Summary.TurnsTaken, 1);
            Assert.AreEqual(10, run.State.Summary.FinalDeckSize);
            Assert.IsNotEmpty(run.State.Summary.Log);
        }
    }
}
