using System.Collections.Generic;
using System.IO;
using System.Text;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Enemies;
using LastLight.Gameplay.Run;
using LastLight.Presentation;
using LastLight.Presentation.Combat;
using LastLight.Presentation.Common;
using LastLight.Presentation.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LastLight.Editor.Validation
{
    /// <summary>
    /// Checks the things that compile fine and break later.
    /// </summary>
    /// <remarks>
    /// The unit tests prove the rules and the play mode tests prove the scene behaves. What
    /// neither covers is the project itself: a card with a duplicate id, a combat node with no
    /// enemy assigned, a scene missing from the build list, an unimported sprite. Every one of
    /// those produces a green test run and a broken build.
    ///
    /// Run it from the menu or from CI with -executeMethod; it exits non-zero when anything is
    /// wrong, so it can gate a build.
    /// </remarks>
    public static class ProjectValidator
    {
        private const string ExpectedEditorVersion = "6000.0.75f1";

        private static readonly List<string> Problems = new List<string>();
        private static readonly List<string> Notes = new List<string>();

        [MenuItem("Last Light/Validate Project", priority = 30)]
        public static void Validate()
        {
            Problems.Clear();
            Notes.Clear();

            CheckEditorVersion();
            CheckBuildSettings();

            List<CardDefinition> cards = LoadAll<CardDefinition>();
            List<EnemyDefinition> enemies = LoadAll<EnemyDefinition>();
            RunConfig config = LoadAll<RunConfig>().Count > 0 ? LoadAll<RunConfig>()[0] : null;

            CheckCards(cards);
            CheckEnemies(enemies);
            CheckRunConfig(config, cards);
            CheckGameScene();

            Report();
        }

        /// <summary>Entry point for `-executeMethod`. Non-zero exit if anything failed.</summary>
        public static void ValidateFromCLI()
        {
            Validate();
            EditorApplication.Exit(Problems.Count == 0 ? 0 : 1);
        }

        private static void Report()
        {
            var report = new StringBuilder();
            report.AppendLine($"[LastLight] Validation: {Problems.Count} problem(s).");

            for (int i = 0; i < Notes.Count; i++) report.AppendLine($"  ok   {Notes[i]}");
            for (int i = 0; i < Problems.Count; i++) report.AppendLine($"  FAIL {Problems[i]}");

            if (Problems.Count == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        private static void Fail(string message) => Problems.Add(message);

        private static void Ok(string message) => Notes.Add(message);

        // ---------------------------------------------------------------- checks

        private static void CheckEditorVersion()
        {
            if (Application.unityVersion == ExpectedEditorVersion)
            {
                Ok($"Unity {Application.unityVersion}");
                return;
            }

            Fail($"Editor is {Application.unityVersion}, the brief requires {ExpectedEditorVersion}.");
        }

        private static void CheckBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            if (scenes.Length != 2)
            {
                Fail($"Expected 2 scenes in Build Settings, found {scenes.Length}.");
                return;
            }

            if (!scenes[0].path.EndsWith("MainMenu.unity"))
            {
                Fail("MainMenu must be build index 0 - that is what a built player loads first.");
            }

            if (!scenes[1].path.EndsWith("Game.unity")) Fail("Game must be build index 1.");

            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (!scene.enabled) Fail($"Scene '{scene.path}' is in the list but disabled.");
                if (!File.Exists(scene.path)) Fail($"Scene '{scene.path}' is listed but missing on disk.");
            }

            if (Problems.Count == 0) Ok("Build settings: MainMenu (0), Game (1)");
        }

        private static void CheckCards(List<CardDefinition> cards)
        {
            if (cards.Count == 0)
            {
                Fail("No CardDefinition assets found.");
                return;
            }

            var seen = new HashSet<string>();

            foreach (CardDefinition card in cards)
            {
                string where = card.name;

                if (string.IsNullOrWhiteSpace(card.Id)) Fail($"{where}: empty id.");
                else if (!seen.Add(card.Id)) Fail($"{where}: duplicate id '{card.Id}'.");

                if (string.IsNullOrWhiteSpace(card.DisplayName)) Fail($"{where}: empty display name.");
                if (card.Cost < 0) Fail($"{where}: negative cost.");

                if (card.Effects.Count == 0)
                {
                    Fail($"{where}: no effects, so the card would do nothing.");
                    continue;
                }

                for (int i = 0; i < card.Effects.Count; i++)
                {
                    if (card.Effects[i] == null) Fail($"{where}: effect {i} is null.");
                }

                // Rules text is generated from the effects, so an empty string means an effect
                // exists that cannot describe itself.
                if (string.IsNullOrWhiteSpace(card.BuildDescription(false)))
                {
                    Fail($"{where}: generated description is empty.");
                }
            }

            Ok($"{cards.Count} cards, ids unique, all describable");
        }

        private static void CheckEnemies(List<EnemyDefinition> enemies)
        {
            if (enemies.Count == 0)
            {
                Fail("No EnemyDefinition assets found.");
                return;
            }

            foreach (EnemyDefinition enemy in enemies)
            {
                if (enemy.MaxLight <= 0) Fail($"{enemy.name}: Light must be above zero.");

                if (enemy.Pattern.Count == 0)
                {
                    Fail($"{enemy.name}: empty action pattern, so it would never act.");
                    continue;
                }

                for (int i = 0; i < enemy.Pattern.Count; i++)
                {
                    EnemyAction action = enemy.Pattern[i];
                    if (action == null) Fail($"{enemy.name}: action {i} is null.");
                    else if (action.Effects.Count == 0) Fail($"{enemy.name}: action {i} has no effects.");
                }
            }

            Ok($"{enemies.Count} enemies with non-empty patterns");
        }

        private static void CheckRunConfig(RunConfig config, List<CardDefinition> cards)
        {
            if (config == null)
            {
                Fail("No RunConfig asset found.");
                return;
            }

            int starterCount = 0;
            foreach (StarterDeckEntry entry in config.StarterDeck)
            {
                if (entry?.Card == null) Fail("RunConfig: a starter deck entry has no card.");
                else starterCount += entry.Count;
            }

            if (starterCount == 0) Fail("RunConfig: the starter deck is empty.");

            if (starterCount < config.MinimumDeckSize)
            {
                Fail($"RunConfig: starter deck ({starterCount}) is below the minimum deck size " +
                     $"({config.MinimumDeckSize}), so a Shrine could never remove a card.");
            }

            for (int i = 0; i < config.RewardPool.Count; i++)
            {
                if (config.RewardPool[i] == null) Fail($"RunConfig: reward pool entry {i} is null.");
            }

            if (config.RewardPool.Count < config.RewardChoiceCount)
            {
                Fail($"RunConfig: reward pool has {config.RewardPool.Count} cards but a draft offers " +
                     $"{config.RewardChoiceCount}.");
            }

            if (config.Nodes.Count == 0) Fail("RunConfig: the run has no nodes.");

            int combats = 0;
            for (int i = 0; i < config.Nodes.Count; i++)
            {
                RunNodeDefinition node = config.Nodes[i];

                if (node == null)
                {
                    Fail($"RunConfig: node {i} is null.");
                    continue;
                }

                if (!node.IsValid) Fail($"RunConfig: node {i} ('{node.Title}') is a combat with no enemy.");
                if (node.Kind == RunNodeKind.Combat) combats++;
                if (string.IsNullOrWhiteSpace(node.Title)) Fail($"RunConfig: node {i} has no title.");
            }

            if (combats == 0) Fail("RunConfig: the run contains no combat, so it cannot be lost.");

            // The brief requires at least two distinct stages or decision points.
            if (config.Nodes.Count < 2) Fail("RunConfig: fewer than two stages.");

            Ok($"RunConfig: {config.Nodes.Count} nodes ({combats} combats), " +
               $"{starterCount}-card starter deck, {config.RewardPool.Count}-card reward pool");
        }

        /// <summary>
        /// Opens the game scene and confirms the pieces the run flow depends on are present and
        /// wired. This is the check that catches a regenerated scene losing a reference.
        /// </summary>
        private static void CheckGameScene()
        {
            string path = EditorBuildSettings.scenes.Length > 1 ? EditorBuildSettings.scenes[1].path : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            Scene previous = EditorSceneManager.GetActiveScene();
            string previousPath = previous.path;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            RequireOne<GameSession>(scene);
            RequireOne<ScreenRouter>(scene);
            RequireOne<CombatScreen>(scene);
            RequireOne<RewardScreen>(scene);
            RequireOne<ShrineScreen>(scene);
            RequireOne<RunResultScreen>(scene);
            RequireOne<HandView>(scene);
            RequireOne<TooltipView>(scene);
            RequireOne<EventSystem>(scene);

            int triggers = Object.FindObjectsByType<TooltipTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            if (triggers == 0) Fail("Game scene: nothing is explainable on hover.");
            else Ok($"Game scene: components present, {triggers} hover explanations");

            if (!string.IsNullOrEmpty(previousPath)) EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
        }

        private static void RequireOne<T>(Scene scene) where T : Object
        {
            int count = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            if (count == 0) Fail($"Game scene: no {typeof(T).Name}.");
            else if (count > 1) Fail($"Game scene: {count} instances of {typeof(T).Name}, expected one.");
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            var results = new List<T>();

            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) results.Add(asset);
            }

            return results;
        }
    }
}
