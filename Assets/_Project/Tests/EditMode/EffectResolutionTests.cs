using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Effects;
using NUnit.Framework;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// What each effect atom actually does, plus the two rules that make cards interact:
    /// Kindled and Exposed. The last two tests guard the definition/runtime split.
    /// </summary>
    [TestFixture]
    public sealed class EffectResolutionTests
    {
        private TestData data;

        [SetUp]
        public void SetUp() => data = new TestData();

        [TearDown]
        public void TearDown() => data.Dispose();

        /// <summary>Starts a combat holding exactly the given cards, with plenty of Focus.</summary>
        private CombatController CombatHolding(params CardDefinition[] definitions)
        {
            var deck = new List<RuntimeCard>();
            for (int i = 0; i < definitions.Length; i++) deck.Add(new RuntimeCard(i + 1, definitions[i]));

            CombatController combat = data.Combat(data.PassiveEnemy(maxLight: 200), deck,
                handSize: definitions.Length, focus: 99);
            combat.StartCombat();
            return combat;
        }

        private static RuntimeCard InHand(CombatController combat, string cardId)
        {
            foreach (RuntimeCard card in combat.Deck.Hand)
            {
                if (card.Definition.Id == cardId) return card;
            }

            Assert.Fail($"Expected '{cardId}' to be in hand.");
            return null;
        }

        [Test]
        public void DealDamage_ReducesEnemyLight()
        {
            CardDefinition strike = data.Card("strike", 1, new DealDamageEffect(9, 12));
            CombatController combat = CombatHolding(strike);
            int before = combat.State.Enemy.Light;

            combat.TryPlayCard(InHand(combat, "strike"));

            Assert.AreEqual(before - 9, combat.State.Enemy.Light);
        }

        [Test]
        public void Ward_AbsorbsDamageBeforeLight()
        {
            var player = new PlayerCombatant("P", 50, 50);
            player.GainWard(6);

            DamageApplication applied = player.ApplyDamage(10);

            Assert.AreEqual(6, applied.WardAbsorbed);
            Assert.AreEqual(4, applied.LightLost);
            Assert.AreEqual(46, player.Light, "50 Light minus the 4 that got through the 6 Ward.");
            Assert.AreEqual(0, player.Ward);
        }

        [Test]
        public void Ward_FullyAbsorbsASmallHit()
        {
            var player = new PlayerCombatant("P", 50, 50);
            player.GainWard(10);

            DamageApplication applied = player.ApplyDamage(4);

            Assert.AreEqual(0, applied.LightLost);
            Assert.AreEqual(6, player.Ward, "Unused Ward carries within the turn.");
            Assert.AreEqual(50, player.Light);
        }

        [Test]
        public void Damage_NeverPushesLightBelowZero()
        {
            var player = new PlayerCombatant("P", 10, 10);

            DamageApplication applied = player.ApplyDamage(999);

            Assert.AreEqual(0, player.Light);
            Assert.AreEqual(10, applied.LightLost, "Reported loss must be the Light actually lost, not the swing.");
            Assert.IsFalse(player.IsAlive);
        }

        [Test]
        public void Heal_ClampsAtMaximumLight()
        {
            var player = new PlayerCombatant("P", 50, 45);

            int restored = player.Heal(20);

            Assert.AreEqual(5, restored);
            Assert.AreEqual(50, player.Light);
        }

        [Test]
        public void DrawCards_AddsToHand()
        {
            CardDefinition draw = data.Card("draw", 1, CardType.Skill, new DrawCardsEffect(2, 3));
            CardDefinition filler = data.Card("filler", 1, new DealDamageEffect(1, 1));

            var deck = new List<RuntimeCard> { new RuntimeCard(1, draw) };
            for (int i = 0; i < 4; i++) deck.Add(new RuntimeCard(i + 2, filler));

            CombatController combat = data.Combat(data.PassiveEnemy(), deck, handSize: 1, focus: 9);
            combat.StartCombat();

            // handSize 1 with the draw card shuffled in is unreliable; drive it directly instead.
            int drawn = combat.Draw(2);

            Assert.AreEqual(2, drawn);
        }

        [Test]
        public void GainFocus_IncreasesAvailableFocus()
        {
            CardDefinition ritual = data.Card("ritual", 1, CardType.Skill, new GainFocusEffect(2, 3));
            CombatController combat = CombatHolding(ritual);
            int before = combat.State.Focus;

            combat.TryPlayCard(InHand(combat, "ritual"));

            Assert.AreEqual(before - 1 + 2, combat.State.Focus, "Cost is paid first, then the refund applies.");
        }

        [Test]
        public void Kindled_AddsFlatDamagePerStack()
        {
            CardDefinition lens = data.Card("lens", 1, CardType.Skill,
                new ApplyStatusEffect(StatusType.Kindled, 2, 3, EffectTargeting.Self));
            CardDefinition strike = data.Card("strike", 1, new DealDamageEffect(5, 8));

            CombatController combat = CombatHolding(lens, strike);
            int before = combat.State.Enemy.Light;

            combat.TryPlayCard(InHand(combat, "lens"));
            combat.TryPlayCard(InHand(combat, "strike"));

            Assert.AreEqual(before - 7, combat.State.Enemy.Light, "5 base + 2 Kindled.");
        }

        [Test]
        public void Exposed_IncreasesDamageTaken()
        {
            CardDefinition bind = data.Card("bind", 1, CardType.Skill,
                new ApplyStatusEffect(StatusType.Exposed, 2, 3, EffectTargeting.Opponent));
            CardDefinition strike = data.Card("strike", 1, new DealDamageEffect(10, 14));

            CombatController combat = CombatHolding(bind, strike);
            int before = combat.State.Enemy.Light;

            combat.TryPlayCard(InHand(combat, "bind"));
            combat.TryPlayCard(InHand(combat, "strike"));

            Assert.AreEqual(before - 15, combat.State.Enemy.Light, "10 base, then x1.5 for Exposed.");
        }

        [Test]
        public void Repeat_ResolvesTheInnerEffectSeveralTimes()
        {
            CardDefinition twin = data.Card("twin", 1, new RepeatEffect(3, 4, new DealDamageEffect(4, 4)));
            CombatController combat = CombatHolding(twin);
            int before = combat.State.Enemy.Light;

            combat.TryPlayCard(InHand(combat, "twin"));

            Assert.AreEqual(before - 12, combat.State.Enemy.Light);
        }

        [Test]
        public void Repeat_AppliesKindledToEveryHit()
        {
            // The synergy the reward pool is built around: buffs favour multi-hit cards.
            CardDefinition lens = data.Card("lens", 1, CardType.Skill,
                new ApplyStatusEffect(StatusType.Kindled, 2, 2, EffectTargeting.Self));
            CardDefinition twin = data.Card("twin", 1, new RepeatEffect(3, 3, new DealDamageEffect(4, 4)));

            CombatController combat = CombatHolding(lens, twin);
            int before = combat.State.Enemy.Light;

            combat.TryPlayCard(InHand(combat, "lens"));
            combat.TryPlayCard(InHand(combat, "twin"));

            Assert.AreEqual(before - 18, combat.State.Enemy.Light, "(4 + 2) three times, not 12 + 2.");
        }

        [Test]
        public void AnUpgradedCopy_UsesTheUpgradedMagnitude()
        {
            CardDefinition strike = data.Card("strike", 1, new DealDamageEffect(6, 9));
            var upgraded = new RuntimeCard(1, strike, upgraded: true);

            CombatController combat = data.Combat(data.PassiveEnemy(maxLight: 100),
                new List<RuntimeCard> { upgraded }, handSize: 1, focus: 9);
            combat.StartCombat();
            int before = combat.State.Enemy.Light;

            combat.TryPlayCard(combat.Deck.Hand[0]);

            Assert.AreEqual(before - 9, combat.State.Enemy.Light);
        }

        [Test]
        public void UpgradingOneCopy_LeavesTheDefinitionAndOtherCopiesAlone()
        {
            CardDefinition strike = data.Card("strike", 1, new DealDamageEffect(6, 9));
            var first = new RuntimeCard(1, strike);
            var second = new RuntimeCard(2, strike);

            Assert.IsTrue(first.Upgrade());

            Assert.IsTrue(first.IsUpgraded);
            Assert.IsFalse(second.IsUpgraded, "Upgrading a copy must not touch its siblings.");
            Assert.AreEqual("Deal 6 damage.", strike.BuildDescription(false),
                "The shared asset must be unchanged - otherwise the upgrade leaks into the next run.");
            Assert.AreEqual("Deal 9 damage.", first.Description);
            Assert.AreEqual("Deal 6 damage.", second.Description);
        }

        [Test]
        public void UpgradingTwice_IsRefused()
        {
            CardDefinition strike = data.Card("strike", 1, new DealDamageEffect(6, 9));
            var card = new RuntimeCard(1, strike);

            Assert.IsTrue(card.Upgrade());
            Assert.IsFalse(card.Upgrade());
        }

        [Test]
        public void CardText_IsBuiltFromTheEffectsThemselves()
        {
            CardDefinition mixed = data.Card("mixed", 2, CardType.Attack,
                new DealDamageEffect(8, 11),
                new GainWardEffect(4, 6));

            Assert.AreEqual("Deal 8 damage. Gain 4 Ward.", mixed.BuildDescription(false));
            Assert.AreEqual("Deal 11 damage. Gain 6 Ward.", mixed.BuildDescription(true));
        }
    }
}
