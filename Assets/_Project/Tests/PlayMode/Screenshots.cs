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
    /// Two reasons this exists. Development here is headless, so without it the only way to know
    /// what the game looks like is to ask someone to open the editor - and a bug that is only
    /// visible (a bar rendering as a smear, an actor the size of a speck) survives a fully green
    /// test suite. It also produces the images for the README.
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

            // Play a card and take a hit so the shot shows a fight in progress.
            foreach (var card in new System.Collections.Generic.List<Gameplay.Cards.RuntimeCard>(
                         session.Combat.Deck.Hand))
            {
                if (session.TryPlayCard(card).Success) break;
            }

            session.EndTurn();
            yield return null;
            yield return Capture("03-combat-turn-two");

            // --- draft -----------------------------------------------------
            session.Combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;
            yield return Capture("04-stage-cleared");

            Click(screen.Overlay, "ActionButton");
            yield return null;
            yield return Capture("05-card-reward");

            // --- shrine ----------------------------------------------------
            var reward = Object.FindFirstObjectByType<Presentation.Run.RewardScreen>(FindObjectsInactive.Include);
            Click(reward, "SkipButton");
            yield return null;

            session.Combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;
            Click(screen.Overlay, "ActionButton");
            yield return null;
            yield return Capture("06-shrine");

            var shrine = Object.FindFirstObjectByType<Presentation.Run.ShrineScreen>(FindObjectsInactive.Include);
            Click(shrine, "UpgradeButton");
            yield return null;
            yield return Capture("07-shrine-choosing");

            Click(shrine, "LeaveButton");
            yield return null;

            // --- run summary -----------------------------------------------
            session.Combat.DebugEndCombat(CombatOutcome.Victory);
            yield return null;
            Click(screen.Overlay, "ActionButton");
            yield return null;
            yield return Capture("08-run-summary");

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
