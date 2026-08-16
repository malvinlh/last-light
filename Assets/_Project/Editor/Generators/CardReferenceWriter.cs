using System.Collections.Generic;
using System.IO;
using System.Text;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Enemies;
using LastLight.Gameplay.Run;
using UnityEditor;
using UnityEngine;

namespace LastLight.Editor.Generators
{
    /// <summary>
    /// Writes the card and enemy reference document from the generated assets.
    /// </summary>
    /// <remarks>
    /// Generated rather than hand-written for the same reason the card text itself is generated:
    /// a balance change should not be able to leave the documentation quietly wrong. This reads
    /// the assets - the runtime source of truth - not the authoring catalog, so what it prints is
    /// what the game will actually load.
    /// </remarks>
    public static class CardReferenceWriter
    {
        private const string OutputPath = "Documentation/CARD-REFERENCE.md";

        [MenuItem("Last Light/Write Card Reference", priority = 31)]
        public static void Write()
        {
            RunConfig config = Load<RunConfig>();
            List<CardDefinition> cards = LoadAll<CardDefinition>();
            List<EnemyDefinition> enemies = LoadAll<EnemyDefinition>();

            cards.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var starterCounts = new Dictionary<string, int>();
            if (config != null)
            {
                foreach (StarterDeckEntry entry in config.StarterDeck)
                {
                    if (entry?.Card != null) starterCounts[entry.Card.Id] = entry.Count;
                }
            }

            var doc = new StringBuilder();
            doc.AppendLine("# Card and enemy reference");
            doc.AppendLine();
            doc.AppendLine("Generated from the ScriptableObject assets by **Last Light → Write Card Reference**.");
            doc.AppendLine("Do not edit by hand; change the catalog in `Assets/_Project/Editor/Generators/`,");
            doc.AppendLine("regenerate the data, then regenerate this.");
            doc.AppendLine();
            doc.AppendLine("Rules text is produced by the cards' own effects, so what is printed here is exactly");
            doc.AppendLine("what the card shows in game. The upgraded column is what a Shrine sharpening turns it into.");
            doc.AppendLine();

            AppendStarter(doc, cards, starterCounts);
            AppendRewardPool(doc, config, cards);
            AppendEnemies(doc, enemies);
            AppendRun(doc, config);

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Documentation");
            File.WriteAllText(OutputPath, doc.ToString());

            Debug.Log($"[LastLight] Wrote {OutputPath} ({cards.Count} cards, {enemies.Count} enemies).");
        }

        public static void WriteFromCLI()
        {
            Write();
            EditorApplication.Exit(File.Exists(OutputPath) ? 0 : 1);
        }

        private static void AppendStarter(StringBuilder doc, List<CardDefinition> cards,
            Dictionary<string, int> starterCounts)
        {
            doc.AppendLine("## Starter deck");
            doc.AppendLine();
            doc.AppendLine("| Copies | Card | Cost | Type | Effect | Sharpened |");
            doc.AppendLine("|---|---|---|---|---|---|");

            foreach (CardDefinition card in cards)
            {
                if (!starterCounts.TryGetValue(card.Id, out int count)) continue;
                doc.AppendLine(Row(card, $"{count}x"));
            }

            doc.AppendLine();
        }

        private static void AppendRewardPool(StringBuilder doc, RunConfig config, List<CardDefinition> cards)
        {
            doc.AppendLine("## Reward pool");
            doc.AppendLine();
            doc.AppendLine("Drafted one of three after a victory.");
            doc.AppendLine();
            doc.AppendLine("| | Card | Cost | Type | Effect | Sharpened |");
            doc.AppendLine("|---|---|---|---|---|---|");

            var pool = new HashSet<string>();
            if (config != null)
            {
                foreach (CardDefinition card in config.RewardPool)
                {
                    if (card != null) pool.Add(card.Id);
                }
            }

            foreach (CardDefinition card in cards)
            {
                if (pool.Contains(card.Id)) doc.AppendLine(Row(card, string.Empty));
            }

            doc.AppendLine();
        }

        private static string Row(CardDefinition card, string lead) =>
            $"| {lead} | **{card.DisplayName}** | {card.Cost} | {card.CardType} | " +
            $"{card.BuildDescription(false)} | {card.BuildDescription(true)} |";

        private static void AppendEnemies(StringBuilder doc, List<EnemyDefinition> enemies)
        {
            doc.AppendLine("## Enemies");
            doc.AppendLine();
            doc.AppendLine("Patterns loop, and the next action is always telegraphed a turn ahead.");
            doc.AppendLine();

            foreach (EnemyDefinition enemy in enemies)
            {
                doc.AppendLine($"### {enemy.DisplayName} ({enemy.MaxLight} Light)");
                doc.AppendLine();
                if (!string.IsNullOrWhiteSpace(enemy.Description)) doc.AppendLine($"*{enemy.Description}*");
                doc.AppendLine();
                doc.AppendLine("| # | Intent | Action | Effect |");
                doc.AppendLine("|---|---|---|---|");

                for (int i = 0; i < enemy.Pattern.Count; i++)
                {
                    EnemyAction action = enemy.Pattern[i];
                    if (action == null) continue;

                    var effects = new StringBuilder();
                    for (int e = 0; e < action.Effects.Count; e++)
                    {
                        if (action.Effects[e] == null) continue;
                        if (effects.Length > 0) effects.Append(' ');
                        effects.Append(action.Effects[e].Describe(false));
                    }

                    doc.AppendLine($"| {i + 1} | {action.Intent} | {action.Label} | {effects} |");
                }

                doc.AppendLine();
            }
        }

        private static void AppendRun(StringBuilder doc, RunConfig config)
        {
            if (config == null) return;

            doc.AppendLine("## Run layout");
            doc.AppendLine();
            doc.AppendLine($"Starting Light **{config.StartingLight}** · hand **{config.CombatRules.HandSize}** · " +
                           $"Focus **{config.CombatRules.FocusPerTurn}** per turn · " +
                           $"draft offers **{config.RewardChoiceCount}** · " +
                           $"rest restores **{config.ShrineMendAmount}** · " +
                           $"deck floor **{config.MinimumDeckSize}**");
            doc.AppendLine();
            doc.AppendLine("| Stage | Kind | Title | Enemy |");
            doc.AppendLine("|---|---|---|---|");

            for (int i = 0; i < config.Nodes.Count; i++)
            {
                RunNodeDefinition node = config.Nodes[i];
                if (node == null) continue;

                string enemy = node.Enemy != null ? node.Enemy.DisplayName : "-";
                doc.AppendLine($"| {i + 1} | {node.Kind} | {node.Title} | {enemy} |");
            }

            doc.AppendLine();
        }

        private static T Load<T>() where T : Object
        {
            List<T> all = LoadAll<T>();
            return all.Count > 0 ? all[0] : null;
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
