using System.Collections;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Presentation;
using LastLight.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastLight.Tests.PlayMode
{
    /// <summary>
    /// Plays the generated scene the way a player would.
    /// </summary>
    /// <remarks>
    /// The EditMode suite proves the rules; this proves the scene. Because the whole UI is
    /// produced by an editor tool, the realistic failure here is not bad logic but an
    /// unassigned serialized reference - the kind of break that compiles, passes every unit
    /// test, and only shows up as a dead button in the build.
    ///
    /// So these drive the real GameSession in the real scene and assert on observable results,
    /// including clicking the actual overlay button rather than calling the method behind it.
    /// </remarks>
    [TestFixture]
    public sealed class VerticalSliceTests
    {
        private GameSession session;
        private CombatScreen screen;

        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // let Start() run

            session = Object.FindFirstObjectByType<GameSession>();
            screen = Object.FindFirstObjectByType<CombatScreen>();

            Assert.IsNotNull(session, "The scene must contain a GameSession.");
            Assert.IsNotNull(screen, "The scene must contain a CombatScreen.");
        }

        [UnityTest]
        public IEnumerator TheSceneOpensIntoAStartedCombat()
        {
            Assert.IsNotNull(session.Run, "The session should have built a run on Start.");
            Assert.IsNotNull(session.Combat, "Stage one is a combat, so a combat should be running.");

            CombatState state = session.Combat.State;

            Assert.AreEqual(CombatPhase.PlayerAction, state.Phase);
            Assert.AreEqual(1, state.TurnNumber);
            Assert.AreEqual(3, state.Focus, "Focus per turn comes from the run config.");
            Assert.AreEqual(5, session.Combat.Deck.Hand.Count, "Hand size comes from the run config.");
            Assert.AreEqual(10, session.Combat.Deck.TotalCards, "The starter deck is ten cards.");
            Assert.IsTrue(state.IsPlayerInputAllowed);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheHandIsRenderedAsOneCardViewPerCard()
        {
            var handView = Object.FindFirstObjectByType<HandView>();
            Assert.IsNotNull(handView, "The scene must contain a HandView.");

            yield return null;

            int visible = 0;
            foreach (CardView view in handView.GetComponentsInChildren<CardView>(false))
            {
                if (view.Card != null) visible++;
            }

            Assert.AreEqual(session.Combat.Deck.Hand.Count, visible,
                "Every card in hand should have a visible card view.");
        }

        [UnityTest]
        public IEnumerator PlayingACardThroughTheSessionHurtsTheEnemy()
        {
            CombatController combat = session.Combat;
            RuntimeCard attack = FirstPlayableAttack(combat);
            Assert.IsNotNull(attack, "The opening hand should contain a playable attack.");

            int enemyLightBefore = combat.State.Enemy.Light;
            int focusBefore = combat.State.Focus;

            PlayCardResult result = session.TryPlayCard(attack);

            Assert.IsTrue(result.Success, $"Expected the play to be accepted, got {result.Rejection}.");
            Assert.Less(combat.State.Enemy.Light, enemyLightBefore, "The enemy should have taken damage.");
            Assert.AreEqual(focusBefore - attack.Cost, combat.State.Focus);
            CollectionAssert.Contains(combat.Deck.DiscardPile, attack);

            yield return null;
        }

        [UnityTest]
        public IEnumerator AnUnaffordableCardIsRefusedAndCostsNothing()
        {
            CombatController combat = session.Combat;

            // Spend the turn's Focus, then try to play whatever is left.
            int guard = 0;
            while (combat.State.Focus > 0 && guard++ < 10)
            {
                RuntimeCard playable = FirstPlayable(combat);
                if (playable == null) break;
                session.TryPlayCard(playable);
            }

            if (combat.Deck.Hand.Count == 0 || combat.State.Outcome != CombatOutcome.InProgress)
            {
                Assert.Ignore("Hand emptied or combat ended before Focus ran out; nothing to assert.");
            }

            RuntimeCard leftover = combat.Deck.Hand[0];
            int handBefore = combat.Deck.Hand.Count;

            PlayCardResult result = session.TryPlayCard(leftover);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlayRejection.NotEnoughFocus, result.Rejection);
            Assert.AreEqual(handBefore, combat.Deck.Hand.Count, "A refused card stays in hand.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickingAnUnaffordableCardExplainsWhyItWasRefused()
        {
            CombatController combat = session.Combat;

            int guard = 0;
            while (combat.State.Focus > 0 && guard++ < 10)
            {
                RuntimeCard playable = FirstPlayable(combat);
                if (playable == null) break;
                session.TryPlayCard(playable);
            }

            yield return null;

            if (combat.Deck.Hand.Count == 0 || combat.State.Outcome != CombatOutcome.InProgress)
            {
                Assert.Ignore("Hand emptied or combat ended before Focus ran out.");
            }

            CardView unaffordable = null;
            foreach (CardView view in Object.FindFirstObjectByType<HandView>().GetComponentsInChildren<CardView>(false))
            {
                if (view.Card != null && !view.Playable) unaffordable = view;
            }

            Assert.IsNotNull(unaffordable, "Expected at least one card the player cannot afford.");

            Button cardButton = unaffordable.GetComponent<Button>();
            Assert.IsTrue(cardButton.interactable,
                "An unaffordable card must still take the click, otherwise the refusal is never explained.");

            cardButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(screen.Toast.IsVisible, "Refusing a card must tell the player why.");
            Assert.AreEqual("Not enough Focus.", screen.Toast.Message);
        }

        [UnityTest]
        public IEnumerator EndingTheTurnLetsTheEnemyActAndDealsAFreshHand()
        {
            CombatController combat = session.Combat;
            int lightBefore = combat.State.Player.Light;
            int predicted = combat.PreviewIntentValue();

            session.EndTurn();
            yield return null;

            Assert.AreEqual(2, combat.State.TurnNumber);
            Assert.AreEqual(5, combat.Deck.Hand.Count, "A new hand is dealt each turn.");
            Assert.AreEqual(CombatPhase.PlayerAction, combat.State.Phase);

            // Stage one opens by attacking, so the telegraphed number should have landed.
            Assert.AreEqual(lightBefore - predicted, combat.State.Player.Light,
                "The damage taken must match the number the intent advertised.");
        }

        [UnityTest]
        public IEnumerator ClearingTheStageRaisesTheOverlayAndStopsAcceptingInput()
        {
            CombatController combat = session.Combat;
            combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;

            Assert.AreEqual(CombatOutcome.Victory, combat.State.Outcome);
            Assert.IsTrue(screen.Overlay.IsVisible, "Clearing a stage should show the overlay.");
            Assert.IsFalse(combat.State.IsPlayerInputAllowed);

            RuntimeCard anyCard = combat.Deck.Hand.Count > 0 ? combat.Deck.Hand[0] : null;
            if (anyCard != null)
            {
                PlayCardResult result = session.TryPlayCard(anyCard);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(PlayRejection.CombatOver, result.Rejection);
            }
        }

        // What happens after the overlay - continuing to the next node, losing the run, and
        // starting over from the summary - belongs to the run flow and is covered by
        // RunLoopTests, which drives those screens rather than just this one fight.

        [UnityTest]
        public IEnumerator EveryKeyPanelHasSizeAndSitsInsideTheCanvas()
        {
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "The scene must have a Canvas.");

            var canvasRect = (RectTransform)canvas.transform;
            Rect bounds = canvasRect.rect;

            foreach (string name in new[]
                     {
                         "PlayerPanel", "EnemyPanel", "HandTray", "EndTurnButton",
                         "FocusBox", "DrawBox", "DiscardBox", "StageLabel"
                     })
            {
                RectTransform element = FindRect(canvasRect, name);
                Assert.IsNotNull(element, $"'{name}' is missing from the generated scene.");

                Rect r = element.rect;
                Assert.Greater(r.width, 1f, $"'{name}' has no width.");
                Assert.Greater(r.height, 1f, $"'{name}' has no height.");

                // Centre expressed in canvas space, so this is resolution independent.
                Vector2 centre = canvasRect.InverseTransformPoint(element.TransformPoint(r.center));
                Assert.IsTrue(bounds.Contains(centre),
                    $"'{name}' is centred at {centre}, outside the canvas {bounds}.");
            }
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            foreach (RectTransform candidate in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (candidate.name == name) return candidate;
            }

            return null;
        }

        // ---------------------------------------------------------------- helpers

        private static RuntimeCard FirstPlayable(CombatController combat)
        {
            foreach (RuntimeCard card in new List<RuntimeCard>(combat.Deck.Hand))
            {
                if (combat.ValidatePlay(card).Success) return card;
            }

            return null;
        }

        private static RuntimeCard FirstPlayableAttack(CombatController combat)
        {
            foreach (RuntimeCard card in new List<RuntimeCard>(combat.Deck.Hand))
            {
                if (card.CardType == CardType.Attack && combat.ValidatePlay(card).Success) return card;
            }

            return null;
        }
    }
}
