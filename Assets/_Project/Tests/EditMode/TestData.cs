using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Common;
using LastLight.Gameplay.Effects;
using LastLight.Gameplay.Enemies;
using LastLight.Gameplay.Run;
using UnityEngine;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// Builders for the ScriptableObject data the gameplay layer expects.
    /// </summary>
    /// <remarks>
    /// Tests construct their own definitions rather than loading the shipped assets, so a
    /// balance change to a real card can never turn a rules test red. Instances are tracked
    /// and destroyed in teardown to keep Unity from reporting leaked objects.
    /// </remarks>
    internal sealed class TestData
    {
        private readonly List<Object> created = new List<Object>();

        public CardDefinition Card(string id, int cost, params CardEffect[] effects) =>
            Card(id, cost, CardType.Attack, effects);

        public CardDefinition Card(string id, int cost, CardType type, params CardEffect[] effects)
        {
            var definition = ScriptableObject.CreateInstance<CardDefinition>();
            definition.Configure(id, id, cost, type, effects);
            created.Add(definition);
            return definition;
        }

        public EnemyDefinition Enemy(string id, int maxLight, params EnemyAction[] pattern)
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.Configure(id, id, maxLight, pattern);
            created.Add(definition);
            return definition;
        }

        /// <summary>An enemy that does nothing on its turn - used when the test is not about the enemy.</summary>
        public EnemyDefinition PassiveEnemy(string id = "dummy", int maxLight = 100) =>
            Enemy(id, maxLight, new EnemyAction("Wait", IntentKind.Defend, new CardEffect[0]));

        public EnemyAction Attack(int damage) =>
            new EnemyAction("Attack", IntentKind.Attack,
                new CardEffect[] { new DealDamageEffect(damage, damage, EffectTargeting.Opponent) });

        public EnemyAction Defend(int ward) =>
            new EnemyAction("Defend", IntentKind.Defend,
                new CardEffect[] { new GainWardEffect(ward, ward, EffectTargeting.Self) });

        public RunConfig RunConfig(IEnumerable<StarterDeckEntry> starter, IEnumerable<CardDefinition> rewards,
            IEnumerable<RunNodeDefinition> nodes, int light = 50, int handSize = 5, int focus = 3,
            int rewardChoices = 3, int mendAmount = 12, int minDeckSize = 5)
        {
            var config = ScriptableObject.CreateInstance<RunConfig>();
            config.Configure(light, new CombatRules(handSize, focus), starter, rewards, nodes,
                rewardChoices, mendAmount, minDeckSize);
            created.Add(config);
            return config;
        }

        /// <summary>Builds a combat directly, bypassing the run layer.</summary>
        public CombatController Combat(EnemyDefinition enemy, IEnumerable<RuntimeCard> deck,
            int playerLight = 50, int handSize = 5, int focus = 3, int seed = 1)
        {
            var player = new PlayerCombatant("Tester", playerLight, playerLight);
            return new CombatController(player, enemy, deck, new CombatRules(handSize, focus), new GameRng(seed));
        }

        /// <summary>Makes <paramref name="count"/> distinct runtime copies of one definition.</summary>
        public List<RuntimeCard> Copies(CardDefinition definition, int count)
        {
            var cards = new List<RuntimeCard>(count);
            for (int i = 0; i < count; i++) cards.Add(new RuntimeCard(i + 1, definition));
            return cards;
        }

        public void Dispose()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }
    }
}
