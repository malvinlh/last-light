using System.Collections;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Run;
using LastLight.Presentation;
using LastLight.Presentation.Combat;
using LastLight.Presentation.Common;
using LastLight.Presentation.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastLight.Tests.PlayMode
{
    /// <summary>
    /// Walks a whole run through the real screens: fight, draft, fight, shrine, fight, result.
    /// </summary>
    /// <remarks>
    /// These drive the UI the way a player does - clicking the actual buttons and card views -
    /// because the run flow is mostly wiring, and wiring is what silently breaks. The rules
    /// underneath are already covered by the EditMode suite, so combats here are ended with the
    /// development shortcut to keep the tests about routing rather than about winning fights.
    /// </remarks>
    [TestFixture]
    public sealed class RunLoopTests
    {
        private GameSession session;
        private CombatScreen combatScreen;
        private RewardScreen rewardScreen;
        private ShrineScreen shrineScreen;
        private RunResultScreen resultScreen;

        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            session = Object.FindFirstObjectByType<GameSession>();
            combatScreen = Object.FindFirstObjectByType<CombatScreen>(FindObjectsInactive.Include);
            rewardScreen = Object.FindFirstObjectByType<RewardScreen>(FindObjectsInactive.Include);
            shrineScreen = Object.FindFirstObjectByType<ShrineScreen>(FindObjectsInactive.Include);
            resultScreen = Object.FindFirstObjectByType<RunResultScreen>(FindObjectsInactive.Include);

            Assert.IsNotNull(session);
            Assert.IsNotNull(rewardScreen, "The scene must contain a RewardScreen.");
            Assert.IsNotNull(shrineScreen, "The scene must contain a ShrineScreen.");
            Assert.IsNotNull(resultScreen, "The scene must contain a RunResultScreen.");
        }

        // ---------------------------------------------------------------- node routing

        [UnityTest]
        public IEnumerator TheRunOpensOnACombatNode()
        {
            Assert.AreEqual(RunNodeKind.Combat, session.Run.CurrentNode.Kind);
            Assert.IsNotNull(session.Combat);
            Assert.IsTrue(IsShowing(combatScreen), "Combat should be the visible screen.");
            Assert.IsFalse(IsShowing(rewardScreen));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearingAStageRoutesToTheRewardDraft()
        {
            yield return WinCurrentStage();

            Assert.AreEqual(RunNodeKind.CardReward, session.Run.CurrentNode.Kind);
            Assert.IsTrue(IsShowing(rewardScreen), "The draft should be the visible screen.");
            Assert.IsFalse(IsShowing(combatScreen), "Combat should have been hidden.");
            Assert.AreEqual(3, OfferedCards().Count, "The draft offers three cards.");
        }

        [UnityTest]
        public IEnumerator ADraftedCardIsInTheDeckTheNextStageIsPlayedWith()
        {
            yield return WinCurrentStage();

            List<CardView> offered = OfferedCards();
            CardDefinition chosen = offered[0].Card.Definition;

            Click(offered[0].GetComponent<Button>());
            yield return null;

            Assert.AreEqual(11, session.Run.State.Deck.Count, "The drafted card joins the run deck.");
            Assert.AreEqual(RunNodeKind.Combat, session.Run.CurrentNode.Kind);
            Assert.AreEqual(11, session.Combat.Deck.TotalCards, "Stage two is played with the new deck.");

            bool present = false;
            foreach (RuntimeCard card in session.Run.State.Deck) present |= card.Definition == chosen;
            Assert.IsTrue(present, "The exact card that was drafted should be in the deck.");
        }

        [UnityTest]
        public IEnumerator SkippingTheDraftTakesNothing()
        {
            yield return WinCurrentStage();

            Click(FindButton(rewardScreen, "SkipButton"));
            yield return null;

            Assert.AreEqual(10, session.Run.State.Deck.Count);
            Assert.AreEqual(0, session.Run.State.Summary.CardsAdded);
            Assert.AreEqual(RunNodeKind.Combat, session.Run.CurrentNode.Kind);
        }

        // ---------------------------------------------------------------- the shrine

        [UnityTest]
        public IEnumerator TheShrineIsReachedAfterTheSecondStage()
        {
            yield return ReachTheShrine();

            Assert.AreEqual(RunNodeKind.Shrine, session.Run.CurrentNode.Kind);
            Assert.IsTrue(IsShowing(shrineScreen));
        }

        [UnityTest]
        public IEnumerator TheShrineSharpensExactlyOneCopy()
        {
            yield return ReachTheShrine();

            Click(FindButton(shrineScreen, "UpgradeButton"));
            yield return null;

            List<CardView> deckCards = TrayCards(shrineScreen);
            Assert.Greater(deckCards.Count, 0, "The shrine should list the deck.");

            RuntimeCard target = deckCards[0].Card;
            CardDefinition definition = target.Definition;

            Click(deckCards[0].GetComponent<Button>());
            yield return null;

            Assert.IsTrue(target.IsUpgraded);
            Assert.AreEqual(1, session.Run.State.Summary.CardsUpgraded);

            int upgraded = 0;
            foreach (RuntimeCard card in session.Run.State.Deck)
            {
                if (card.Definition == definition && card.IsUpgraded) upgraded++;
            }

            Assert.AreEqual(1, upgraded, "Only the chosen copy changes, not the card.");
            Assert.AreEqual(RunNodeKind.Combat, session.Run.CurrentNode.Kind, "The shrine advances when used.");
        }

        [UnityTest]
        public IEnumerator TheShrineCanReleaseACard()
        {
            yield return ReachTheShrine();

            int before = session.Run.State.Deck.Count;
            Click(FindButton(shrineScreen, "RemoveButton"));
            yield return null;

            List<CardView> deckCards = TrayCards(shrineScreen);
            Click(deckCards[0].GetComponent<Button>());
            yield return null;

            Assert.AreEqual(before - 1, session.Run.State.Deck.Count);
            Assert.AreEqual(1, session.Run.State.Summary.CardsRemoved);
        }

        [UnityTest]
        public IEnumerator TheShrineCanRestoreLight()
        {
            // Take a hit in stage one first, or resting is a no-op and proves nothing.
            session.EndTurn();
            yield return null;

            yield return ReachTheShrine();

            int before = session.Run.State.Light;
            int maxLight = session.Run.State.MaxLight;
            Assert.Less(before, maxLight, "Precondition: the player should be hurt.");

            Click(FindButton(shrineScreen, "MendButton"));
            yield return null;

            Assert.AreEqual(Mathf.Min(maxLight, before + 12), session.Run.State.Light,
                "Resting restores the configured amount, clamped to maximum Light.");
            Assert.AreEqual(RunNodeKind.Combat, session.Run.CurrentNode.Kind);
        }

        [UnityTest]
        public IEnumerator LeavingTheShrineTakesNothing()
        {
            yield return ReachTheShrine();

            int deck = session.Run.State.Deck.Count;
            Click(FindButton(shrineScreen, "LeaveButton"));
            yield return null;

            Assert.AreEqual(deck, session.Run.State.Deck.Count);
            Assert.AreEqual(0, session.Run.State.Summary.CardsUpgraded);
            Assert.AreEqual(RunNodeKind.Combat, session.Run.CurrentNode.Kind);
        }

        // ---------------------------------------------------------------- ending the run

        [UnityTest]
        public IEnumerator ClearingEveryStageWinsTheRun()
        {
            yield return WinCurrentStage();                            // stage 1
            Click(FindButton(rewardScreen, "SkipButton"));
            yield return null;

            yield return WinCurrentStage();                            // stage 2
            Click(FindButton(shrineScreen, "LeaveButton"));
            yield return null;

            yield return WinCurrentStage();                            // stage 3 (boss)

            Assert.AreEqual(RunOutcome.Victory, session.Run.State.Outcome);
            Assert.IsTrue(IsShowing(resultScreen), "The run summary should be on screen.");
            Assert.AreEqual(3, session.Run.State.Summary.StagesCleared);
        }

        [UnityTest]
        public IEnumerator DyingEndsTheRunStraightAway()
        {
            session.Combat.DebugEndCombat(CombatOutcome.Defeat);
            yield return null;

            Assert.AreEqual(RunOutcome.Defeat, session.Run.State.Outcome);
            Assert.IsTrue(IsShowing(resultScreen));
            Assert.IsFalse(IsShowing(combatScreen), "Combat should not still be visible after a loss.");
            Assert.AreEqual(0, session.Run.State.Summary.StagesCleared);
        }

        [UnityTest]
        public IEnumerator NewRunFromTheSummaryResetsEverything()
        {
            // Change the run, then lose it, so the reset has something to undo.
            yield return WinCurrentStage();
            Click(OfferedCards()[0].GetComponent<Button>());
            yield return null;

            Assert.AreEqual(11, session.Run.State.Deck.Count, "Precondition: the deck grew.");

            session.Combat.DebugEndCombat(CombatOutcome.Defeat);
            yield return null;

            Click(FindButton(resultScreen, "NewRunButton"));
            yield return null;

            Assert.AreEqual(RunOutcome.InProgress, session.Run.State.Outcome);
            Assert.AreEqual(0, session.Run.State.NodeIndex);
            Assert.AreEqual(10, session.Run.State.Deck.Count, "Back to the starter deck.");
            Assert.AreEqual(50, session.Run.State.Light);
            Assert.AreEqual(0, session.Run.State.Summary.CardsAdded);
            Assert.IsTrue(IsShowing(combatScreen), "A new run opens on stage one.");
            Assert.IsFalse(IsShowing(resultScreen));

            foreach (RuntimeCard card in session.Run.State.Deck)
            {
                Assert.IsFalse(card.IsUpgraded, "No upgrade may survive into a new run.");
            }
        }

        [UnityTest]
        public IEnumerator LightCarriesAcrossStages()
        {
            CombatController first = session.Combat;

            // Skip a turn so the Shade lands a hit, then clear the stage.
            int predicted = first.PreviewIntentValue();
            session.EndTurn();
            yield return null;

            int damaged = first.State.Player.Light;
            Assert.AreEqual(50 - predicted, damaged, "Precondition: the enemy should have hit.");

            yield return WinCurrentStage();
            Click(FindButton(rewardScreen, "SkipButton"));
            yield return null;

            Assert.AreEqual(damaged, session.Combat.State.Player.Light,
                "Stage two must start on the Light left over from stage one.");
        }

        // ---------------------------------------------------------------- helpers

        private IEnumerator WinCurrentStage()
        {
            Assert.AreEqual(RunNodeKind.Combat, session.Run.CurrentNode.Kind, "Expected to be in a combat.");

            session.Combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;

            Assert.IsTrue(combatScreen.Overlay.IsVisible, "Clearing a stage should show the overlay.");

            Click(FindButton(combatScreen.Overlay, "ActionButton"));
            yield return null;
        }

        private IEnumerator ReachTheShrine()
        {
            yield return WinCurrentStage();
            Click(FindButton(rewardScreen, "SkipButton"));
            yield return null;

            yield return WinCurrentStage();
        }

        private List<CardView> OfferedCards() => TrayCards(rewardScreen);

        private static List<CardView> TrayCards(Component screen)
        {
            var cards = new List<CardView>();
            var tray = screen.GetComponentInChildren<CardTray>(true);

            foreach (CardView view in tray.GetComponentsInChildren<CardView>(false))
            {
                if (view.Card != null) cards.Add(view);
            }

            return cards;
        }

        private static bool IsShowing(Component screen) =>
            screen != null && screen.gameObject.activeInHierarchy;

        private static Button FindButton(Component root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name) return button;
            }

            return null;
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button, "Expected to find the button being clicked.");
            button.onClick.Invoke();
        }
    }

    /// <summary>The menu is the only scene transition in the game, so it gets its own check.</summary>
    [TestFixture]
    public sealed class MainMenuTests
    {
        [UnityTest]
        public IEnumerator BeginningTheWatchLoadsTheGameScene()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;

            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.IsNotNull(menu, "The menu scene must contain a MainMenuScreen.");

            menu.BeginRun();
            yield return null;
            yield return null;

            Assert.AreEqual("Game", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindFirstObjectByType<GameSession>(),
                "Loading the game scene should start a session.");
        }
    }
}
