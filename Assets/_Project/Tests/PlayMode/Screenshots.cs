using System.Collections;
using System.IO;
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
    /// Captures each screen to a PNG. Marked Explicit, so it never runs as part of the suite.
    /// </summary>
    /// <remarks>
    /// Run with:
    ///   Unity -batchmode -runTests -testPlatform PlayMode -testFilter "Screenshots"
    ///
    /// Two reasons this exists. A bug that is only visible (a bar rendering as a smear, an actor
    /// the size of a speck) survives a fully green test suite, so the images are the only check
    /// that catches it. It also produces the screenshots for the README, which then cannot drift
    /// out of date without the fixture being re-run.
    ///
    /// ScreenCapture does not work in batch mode: there is no swap chain to read back from, so it
    /// silently writes nothing. Rendering the camera into a RenderTexture does work, but overlay
    /// canvases bypass cameras entirely, so the canvas is switched to camera space for the
    /// duration of the shot.
    /// </remarks>
    [TestFixture]
    [Explicit("Writes screenshots; run deliberately, not as part of the suite.")]
    public sealed class Screenshots
    {
        private const int Width = 1600;
        private const int Height = 900;

        private static string OutputFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/screenshots"));

        [UnityTest]
        public IEnumerator CaptureEveryScreen()
        {
            Directory.CreateDirectory(OutputFolder);

            // --- main menu -------------------------------------------------
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return Capture("01-main-menu");

            // --- combat ----------------------------------------------------
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindFirstObjectByType<GameSession>();
            var screen = Object.FindFirstObjectByType<CombatScreen>(FindObjectsInactive.Include);
            Assert.IsNotNull(session);

            yield return Capture("02-combat");

            // A frame with a tooltip open, so its placement can be checked by eye.
            var tooltip = Object.FindFirstObjectByType<Presentation.Common.TooltipView>(
                FindObjectsInactive.Include);
            if (tooltip != null)
            {
                tooltip.ShowAt("Ward absorbs incoming damage. It is spent as it blocks, and whatever " +
                               "is left expires at the start of your next turn.", new Vector2(-620f, 380f));
                yield return null;
                yield return Capture("02b-tooltip");
                tooltip.Hide();
                yield return null;
            }

            // Play three real turns so the shot shows a fight in progress rather than an opening
            // hand: Light spent, Ward up, piles moved, the enemy visibly damaged.
            for (int turn = 0; turn < 3; turn++)
            {
                PlayWholeTurn(session);
                if (session.Combat.State.Outcome != CombatOutcome.InProgress) break;
                session.EndTurn();
                yield return null;
            }

            yield return Capture("03-combat-in-progress");

            // --- draft -----------------------------------------------------
            session.Combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;
            yield return Capture("04-stage-cleared");

            Click(screen.Overlay, "ActionButton");
            yield return null;
            yield return Capture("05-card-reward");

            // Actually take a card, so the deck and the summary reflect a real decision.
            var reward = Object.FindFirstObjectByType<Presentation.Run.RewardScreen>(FindObjectsInactive.Include);
            ClickFirstCard(reward);
            yield return null;

            // --- shrine ----------------------------------------------------
            PlayWholeTurn(session);
            session.Combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;
            Click(screen.Overlay, "ActionButton");
            yield return null;
            yield return Capture("06-shrine");

            var shrine = Object.FindFirstObjectByType<Presentation.Run.ShrineScreen>(FindObjectsInactive.Include);
            Click(shrine, "UpgradeButton");
            yield return null;
            yield return Capture("07-shrine-choosing");

            // Sharpen one, so the summary shows a non-zero count and the deck holds a "+" card.
            ClickFirstCard(shrine);
            yield return null;

            // --- victory summary ---------------------------------------------
            PlayWholeTurn(session);
            session.Combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;
            Click(screen.Overlay, "ActionButton");
            yield return null;
            yield return Capture("08-run-summary-victory");

            // --- defeat summary ----------------------------------------------
            Click(Object.FindFirstObjectByType<Presentation.Run.RunResultScreen>(FindObjectsInactive.Include),
                "NewRunButton");
            yield return null;

            session.Combat.DebugEndCombat(CombatOutcome.Defeat);
            yield return null;
            yield return Capture("09-run-summary-defeat");

            Debug.Log($"[Screenshots] written to {OutputFolder}");
        }

        private static IEnumerator Capture(string name)
        {
            var camera = Object.FindFirstObjectByType<Camera>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (camera == null || canvas == null) yield break;

            RenderMode originalMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            yield return null;

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            image.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), image.EncodeToPNG());

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            canvas.renderMode = originalMode;
            yield return null;
        }

        /// <summary>Plays cards until Focus runs out, the way a player spends a turn.</summary>
        private static void PlayWholeTurn(GameSession session)
        {
            int guard = 0;

            while (guard++ < 12 && session.Combat != null &&
                   session.Combat.State.Outcome == CombatOutcome.InProgress)
            {
                bool played = false;

                foreach (var card in new System.Collections.Generic.List<Gameplay.Cards.RuntimeCard>(
                             session.Combat.Deck.Hand))
                {
                    if (!session.TryPlayCard(card).Success) continue;
                    played = true;
                    break;
                }

                if (!played) return;
            }
        }

        private static void ClickFirstCard(Component screen)
        {
            if (screen == null) return;

            foreach (Presentation.Combat.CardView view in
                     screen.GetComponentsInChildren<Presentation.Combat.CardView>(false))
            {
                if (view.Card == null || !view.Playable) continue;
                view.GetComponent<Button>().onClick.Invoke();
                return;
            }
        }

        private static void Click(Component root, string buttonName)
        {
            if (root == null) return;

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name != buttonName) continue;
                button.onClick.Invoke();
                return;
            }

            Assert.Fail($"No button named '{buttonName}'.");
        }
    }
}
