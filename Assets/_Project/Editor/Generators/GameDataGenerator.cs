using System;
using System.Collections.Generic;
using System.IO;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Effects;
using LastLight.Gameplay.Enemies;
using LastLight.Gameplay.Run;
using UnityEditor;
using UnityEngine;

namespace LastLight.Editor.Generators
{
    /// <summary>
    /// Turns the C# catalogs into the ScriptableObject assets the game actually loads.
    /// </summary>
    /// <remarks>
    /// Idempotent by design, in two senses. Existing assets are reconfigured in place rather
    /// than deleted and recreated, so their GUIDs survive and every reference from the
    /// RunConfig, scenes and prefabs stays intact. And an asset whose authored content already
    /// matches the catalog is left completely untouched.
    ///
    /// That second part is not cosmetic. [SerializeReference] mints new reference ids whenever
    /// the effect list is replaced, so unconditionally rewriting every asset produces a diff of
    /// pure id churn on every run - enough noise to hide a real change during review.
    ///
    /// Order matters - cards, then enemies, then the run config that references both.
    /// </remarks>
    public static class GameDataGenerator
    {
        public const string CardsFolder = "Assets/_Project/Data/Cards";
        public const string EnemiesFolder = "Assets/_Project/Data/Enemies";
        public const string RunFolder = "Assets/_Project/Data/Run";
        public const string RunConfigPath = RunFolder + "/RunConfig_LastLight.asset";

        /// <summary>Counts of what a generation pass actually had to change.</summary>
        private static int assetsCreated;
        private static int assetsUpdated;
        private static int assetsUnchanged;

        [MenuItem("Last Light/Generate Game Data", priority = 0)]
        public static void GenerateAll()
        {
            assetsCreated = 0;
            assetsUpdated = 0;
            assetsUnchanged = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                Dictionary<string, CardDefinition> cards = GenerateCards();
                Dictionary<string, EnemyDefinition> enemies = GenerateEnemies();
                GenerateRunConfig(cards, enemies);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[LastLight] Game data generated: {assetsCreated} created, {assetsUpdated} updated, " +
                      $"{assetsUnchanged} already up to date.");
        }

        /// <summary>Entry point for `-executeMethod`. Sets a non-zero exit code if anything throws.</summary>
        public static void GenerateAllFromCLI()
        {
            try
            {
                GenerateAll();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LastLight] Data generation failed: {exception}");
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- cards

        private static Dictionary<string, CardDefinition> GenerateCards()
        {
            EnsureFolder(CardsFolder);
            var byId = new Dictionary<string, CardDefinition>(CardCatalog.All.Count);

            foreach (CardBlueprint blueprint in CardCatalog.All)
            {
                string path = $"{CardsFolder}/{blueprint.AssetName}.asset";
                CardDefinition asset = LoadOrCreate<CardDefinition>(path);

                CardEffect[] effects = blueprint.Effects();
                string desired = CardDefinition.BuildSignature(blueprint.Id, blueprint.DisplayName, blueprint.Cost,
                    blueprint.Type, effects, blueprint.Upgradable, blueprint.Flavor);

                if (asset.ContentSignature() != desired)
                {
                    asset.Configure(blueprint.Id, blueprint.DisplayName, blueprint.Cost, blueprint.Type,
                        effects, blueprint.Upgradable, blueprint.Flavor);
                    EditorUtility.SetDirty(asset);
                    assetsUpdated++;
                }
                else
                {
                    assetsUnchanged++;
                }

                byId[blueprint.Id] = asset;
            }

            return byId;
        }

        // ---------------------------------------------------------------- enemies

        private static Dictionary<string, EnemyDefinition> GenerateEnemies()
        {
            EnsureFolder(EnemiesFolder);
            var byId = new Dictionary<string, EnemyDefinition>(EnemyCatalog.All.Count);

            foreach (EnemyBlueprint blueprint in EnemyCatalog.All)
            {
                string path = $"{EnemiesFolder}/{blueprint.AssetName}.asset";
                EnemyDefinition asset = LoadOrCreate<EnemyDefinition>(path);

                EnemyAction[] pattern = blueprint.Pattern();
                string desired = EnemyDefinition.BuildSignature(blueprint.Id, blueprint.DisplayName,
                    blueprint.MaxLight, pattern, blueprint.Description, blueprint.Tint);

                if (asset.ContentSignature() != desired)
                {
                    asset.Configure(blueprint.Id, blueprint.DisplayName, blueprint.MaxLight, pattern,
                        blueprint.Description, blueprint.Tint);
                    EditorUtility.SetDirty(asset);
                    assetsUpdated++;
                }
                else
                {
                    assetsUnchanged++;
                }

                byId[blueprint.Id] = asset;
            }

            return byId;
        }

        // ---------------------------------------------------------------- run config

        /// <summary>
        /// Builds the run itself: 50 Light, a 10 card starter deck, and five stops alternating
        /// combat with a decision. The shape of the run lives here and in the asset, never in
        /// gameplay code.
        /// </summary>
        private static void GenerateRunConfig(IReadOnlyDictionary<string, CardDefinition> cards,
            IReadOnlyDictionary<string, EnemyDefinition> enemies)
        {
            EnsureFolder(RunFolder);
            RunConfig config = LoadOrCreate<RunConfig>(RunConfigPath);

            var starter = new List<StarterDeckEntry>();
            var rewardPool = new List<CardDefinition>();

            foreach (CardBlueprint blueprint in CardCatalog.All)
            {
                if (!cards.TryGetValue(blueprint.Id, out CardDefinition card)) continue;

                if (blueprint.StarterCount > 0) starter.Add(new StarterDeckEntry(card, blueprint.StarterCount));
                if (blueprint.InRewardPool) rewardPool.Add(card);
            }

            var nodes = new List<RunNodeDefinition>
            {
                new RunNodeDefinition(RunNodeKind.Combat, "The First Watch",
                    "Something thin is testing the light.", Enemy(enemies, "fledgling_shade")),

                new RunNodeDefinition(RunNodeKind.CardReward, "Salvage",
                    "Take one thing from the wreckage."),

                new RunNodeDefinition(RunNodeKind.Combat, "The Second Watch",
                    "The ground itself has gone hungry.", Enemy(enemies, "grasping_mire")),

                new RunNodeDefinition(RunNodeKind.Shrine, "The Old Shrine",
                    "Sharpen one thing, let go of one thing, or simply rest."),

                new RunNodeDefinition(RunNodeKind.Combat, "The Last Watch",
                    "It has been waiting since the lighthouse was raised.", Enemy(enemies, "devouring_dark"))
            };

            var rules = new CombatRules(handSize: 5, focusPerTurn: 3);
            string desired = RunConfig.BuildSignature(50, rules, starter, rewardPool, nodes, 3, 12, 5);

            if (config.ContentSignature() != desired)
            {
                config.Configure(
                    light: 50,
                    rules: rules,
                    starter: starter,
                    rewards: rewardPool,
                    runNodes: nodes,
                    rewardChoices: 3,
                    mendAmount: 12,
                    minDeckSize: 5);

                EditorUtility.SetDirty(config);
                assetsUpdated++;
            }
            else
            {
                assetsUnchanged++;
            }
        }

        private static EnemyDefinition Enemy(IReadOnlyDictionary<string, EnemyDefinition> enemies, string id)
        {
            if (enemies.TryGetValue(id, out EnemyDefinition enemy)) return enemy;

            Debug.LogError($"[LastLight] Run config references unknown enemy id '{id}'.");
            return null;
        }

        // ---------------------------------------------------------------- helpers

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            assetsCreated++;
            return asset;
        }
    }
}
