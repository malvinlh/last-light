using System;
using System.Collections.Generic;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Effects;
using LastLight.Gameplay.Enemies;
using UnityEngine;

namespace LastLight.Editor.Generators
{
    /// <summary>One authored enemy, before it becomes an asset.</summary>
    internal sealed class EnemyBlueprint
    {
        public EnemyBlueprint(string id, string displayName, int maxLight, string description,
            Color tint, Func<EnemyAction[]> pattern)
        {
            Id = id;
            DisplayName = displayName;
            MaxLight = maxLight;
            Description = description;
            Tint = tint;
            Pattern = pattern;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxLight { get; }
        public string Description { get; }
        public Color Tint { get; }
        public Func<EnemyAction[]> Pattern { get; }

        public string AssetName
        {
            get
            {
                string[] parts = Id.Split('_');
                var name = new System.Text.StringBuilder("Enemy_");

                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Length == 0) continue;
                    name.Append(char.ToUpperInvariant(parts[i][0]));
                    if (parts[i].Length > 1) name.Append(parts[i].Substring(1));
                }

                return name.ToString();
            }
        }
    }

    /// <summary>
    /// The three enemies of a run, authored as fixed looping patterns.
    /// </summary>
    /// <remarks>
    /// Patterns are deliberately deterministic. A telegraphed, repeating sequence turns each
    /// fight into a readable puzzle - the player can plan two turns ahead - and it keeps combat
    /// reproducible in tests, where a random enemy would force every assertion to be a range.
    ///
    /// Enemy actions are built from the same effect atoms as cards, so nothing here is a
    /// special case in the combat code.
    /// </remarks>
    internal static class EnemyCatalog
    {
        public static readonly IReadOnlyList<EnemyBlueprint> All = new List<EnemyBlueprint>
        {
            // Teaching fight: hit, hit, guard. Punishes attacking into the guard turn.
            new EnemyBlueprint("fledgling_shade", "Fledgling Shade", 22,
                "A thin, hungry thing. It has not learned patience yet.",
                new Color(0.45f, 0.42f, 0.62f),
                () => new[]
                {
                    new EnemyAction("Lunge", IntentKind.Attack,
                        new CardEffect[] { new DealDamageEffect(7, 7, EffectTargeting.Opponent) }),
                    new EnemyAction("Lunge", IntentKind.Attack,
                        new CardEffect[] { new DealDamageEffect(7, 7, EffectTargeting.Opponent) }),
                    new EnemyAction("Coil", IntentKind.Defend,
                        new CardEffect[] { new GainWardEffect(6, 6, EffectTargeting.Self) })
                }),

            // Adds a debuff turn: it makes you Exposed, then hits hard into it.
            new EnemyBlueprint("grasping_mire", "Grasping Mire", 32,
                "It does not chase. It waits for the light to come to it.",
                new Color(0.35f, 0.5f, 0.4f),
                () => new[]
                {
                    new EnemyAction("Drag Under", IntentKind.Debuff,
                        new CardEffect[]
                        {
                            new ApplyStatusEffect(StatusType.Exposed, 2, 2, EffectTargeting.Opponent)
                        }),
                    new EnemyAction("Crush", IntentKind.Attack,
                        new CardEffect[] { new DealDamageEffect(9, 9, EffectTargeting.Opponent) }),
                    new EnemyAction("Seep", IntentKind.Attack,
                        new CardEffect[] { new DealDamageEffect(5, 5, EffectTargeting.Opponent) }),
                    new EnemyAction("Harden", IntentKind.Defend,
                        new CardEffect[] { new GainWardEffect(8, 8, EffectTargeting.Self) })
                }),

            // Boss: ramps itself with Kindled, so the fight gets worse the longer it runs.
            new EnemyBlueprint("devouring_dark", "The Devouring Dark", 55,
                "The thing the lighthouse was built against.",
                new Color(0.2f, 0.18f, 0.28f),
                () => new[]
                {
                    new EnemyAction("Gather", IntentKind.Buff,
                        new CardEffect[]
                        {
                            new ApplyStatusEffect(StatusType.Kindled, 2, 2, EffectTargeting.Self)
                        }),
                    new EnemyAction("Swallow", IntentKind.Attack,
                        new CardEffect[] { new DealDamageEffect(8, 8, EffectTargeting.Opponent) }),
                    new EnemyAction("Swallow", IntentKind.Attack,
                        new CardEffect[] { new DealDamageEffect(8, 8, EffectTargeting.Opponent) }),
                    new EnemyAction("Unmake", IntentKind.Debuff,
                        new CardEffect[]
                        {
                            new ApplyStatusEffect(StatusType.Exposed, 2, 2, EffectTargeting.Opponent)
                        }),
                    new EnemyAction("Extinguish", IntentKind.Attack,
                        new CardEffect[] { new DealDamageEffect(12, 12, EffectTargeting.Opponent) })
                })
        };
    }
}
