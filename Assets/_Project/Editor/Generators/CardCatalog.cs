using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Effects;

namespace LastLight.Editor.Generators
{
    /// <summary>One authored card, before it becomes an asset.</summary>
    internal sealed class CardBlueprint
    {
        public CardBlueprint(string id, string displayName, int cost, CardType type,
            Func<CardEffect[]> effects, string flavor = "", int starterCount = 0,
            bool inRewardPool = true, bool upgradable = true)
        {
            Id = id;
            DisplayName = displayName;
            Cost = cost;
            Type = type;
            Effects = effects;
            Flavor = flavor;
            StarterCount = starterCount;
            InRewardPool = inRewardPool;
            Upgradable = upgradable;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int Cost { get; }
        public CardType Type { get; }
        public Func<CardEffect[]> Effects { get; }
        public string Flavor { get; }

        /// <summary>Copies of this card in the starting deck. Zero means it is not a starter.</summary>
        public int StarterCount { get; }

        public bool InRewardPool { get; }
        public bool Upgradable { get; }

        /// <summary>"ember_strike" becomes "Card_EmberStrike.asset".</summary>
        public string AssetName
        {
            get
            {
                string[] parts = Id.Split('_');
                var name = new System.Text.StringBuilder("Card_");

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
    /// The single authoritative list of every card in the game.
    /// </summary>
    /// <remarks>
    /// Cards are authored here in C# and generated into ScriptableObject assets rather than
    /// being hand-built in the Inspector. Three reasons: the whole card set is reviewable in
    /// one diff, regenerating is idempotent so a broken asset can always be rebuilt, and
    /// balance changes are a single-file edit instead of twenty asset selections.
    ///
    /// The assets remain the runtime source of truth - nothing loads this table at play time.
    ///
    /// Each effect carries a normal and an upgraded magnitude, so a Shrine upgrade re-reads the
    /// same asset with a different flag rather than needing a second "+" asset per card.
    /// </remarks>
    internal static class CardCatalog
    {
        public static readonly IReadOnlyList<CardBlueprint> All = new List<CardBlueprint>
        {
            // ---------------------------------------------------------- starter deck (10 cards)
            new CardBlueprint("ember_strike", "Ember Strike", 1, CardType.Attack,
                () => new CardEffect[] { new DealDamageEffect(6, 9) },
                "The oldest trick: swing the lantern.", starterCount: 5, inRewardPool: false),

            new CardBlueprint("ward", "Ward", 1, CardType.Skill,
                () => new CardEffect[] { new GainWardEffect(5, 8) },
                "Cup the flame and turn your back to the wind.", starterCount: 4, inRewardPool: false),

            new CardBlueprint("kindle", "Kindle", 1, CardType.Skill,
                () => new CardEffect[] { new DrawCardsEffect(2, 3) },
                "Feed it. It answers.", starterCount: 1, inRewardPool: false),

            // ---------------------------------------------------------- reward pool (12 cards)
            new CardBlueprint("lantern_flare", "Lantern Flare", 2, CardType.Attack,
                () => new CardEffect[] { new DealDamageEffect(12, 16) },
                "Everything the dark hates, all at once."),

            new CardBlueprint("twin_spark", "Twin Spark", 1, CardType.Attack,
                () => new CardEffect[] { new RepeatEffect(2, 3, new DealDamageEffect(4, 4)) },
                "Two small strikes beat one wide swing - if you are Kindled."),

            new CardBlueprint("focus_lens", "Focus Lens", 1, CardType.Skill,
                () => new CardEffect[] { new ApplyStatusEffect(StatusType.Kindled, 2, 3, EffectTargeting.Self) },
                "Narrow the beam until it bites."),

            new CardBlueprint("sear", "Sear", 1, CardType.Attack,
                () => new CardEffect[]
                {
                    new DealDamageEffect(4, 6),
                    new ApplyStatusEffect(StatusType.Exposed, 2, 3, EffectTargeting.Opponent)
                },
                "Burn a hole in it and the next blow goes deeper."),

            new CardBlueprint("binding_light", "Binding Light", 1, CardType.Skill,
                () => new CardEffect[]
                {
                    new ApplyStatusEffect(StatusType.Exposed, 3, 4, EffectTargeting.Opponent)
                },
                "Pin it against its own shadow."),

            new CardBlueprint("rekindle", "Rekindle", 1, CardType.Skill,
                () => new CardEffect[] { new HealEffect(6, 9) },
                "There is always one more spark."),

            new CardBlueprint("hearthguard", "Hearthguard", 1, CardType.Skill,
                () => new CardEffect[] { new GainWardEffect(6, 9), new HealEffect(3, 5) },
                "Stand where the fire is and let it work."),

            new CardBlueprint("bulwark", "Bulwark", 2, CardType.Skill,
                () => new CardEffect[] { new GainWardEffect(12, 16) },
                "The dark can wait. So can you."),

            new CardBlueprint("second_wind", "Second Wind", 1, CardType.Skill,
                () => new CardEffect[] { new GainFocusEffect(2, 3) },
                "Breathe. Then do the rest of it."),

            new CardBlueprint("long_watch", "Long Watch", 0, CardType.Skill,
                () => new CardEffect[] { new DrawCardsEffect(1, 2) },
                "Nothing happens for hours. Then it does."),

            new CardBlueprint("surge", "Surge", 2, CardType.Attack,
                () => new CardEffect[] { new DealDamageEffect(5, 7), new DrawCardsEffect(1, 2) },
                "Momentum is its own fuel."),

            new CardBlueprint("smother", "Smother", 2, CardType.Attack,
                () => new CardEffect[] { new DealDamageEffect(8, 11), new GainWardEffect(4, 6) },
                "Press it down and keep your guard up.")
        };
    }
}
