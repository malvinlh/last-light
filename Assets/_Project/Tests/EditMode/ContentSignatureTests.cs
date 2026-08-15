using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Effects;
using NUnit.Framework;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// Guards the fingerprint the editor generators use to decide whether an asset needs
    /// rewriting.
    /// </summary>
    /// <remarks>
    /// This exists because of a real bug: the generator rewrote every asset unconditionally,
    /// and because [SerializeReference] mints new reference ids each time an effect list is
    /// replaced, regenerating churned 18 files with no actual content change.
    ///
    /// The risk now runs the other way - a signature that misses a field would leave a genuinely
    /// stale asset on disk. So every field that can differ between two effects of the same type
    /// gets a test.
    /// </remarks>
    [TestFixture]
    public sealed class ContentSignatureTests
    {
        private TestData data;

        [SetUp]
        public void SetUp() => data = new TestData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void IdenticalContentProducesIdenticalSignatures()
        {
            CardDefinition first = data.Card("strike", 1, new DealDamageEffect(6, 9));
            CardDefinition second = data.Card("strike", 1, new DealDamageEffect(6, 9));

            Assert.AreEqual(first.ContentSignature(), second.ContentSignature(),
                "Regenerating unchanged data must be recognised as a no-op.");
        }

        [Test]
        public void ChangingAMagnitudeChangesTheSignature()
        {
            CardDefinition before = data.Card("strike", 1, new DealDamageEffect(6, 9));
            CardDefinition after = data.Card("strike", 1, new DealDamageEffect(7, 9));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature());
        }

        [Test]
        public void ChangingAnUpgradedMagnitudeChangesTheSignature()
        {
            CardDefinition before = data.Card("strike", 1, new DealDamageEffect(6, 9));
            CardDefinition after = data.Card("strike", 1, new DealDamageEffect(6, 10));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature());
        }

        [Test]
        public void ChangingCostChangesTheSignature()
        {
            CardDefinition before = data.Card("strike", 1, new DealDamageEffect(6, 9));
            CardDefinition after = data.Card("strike", 2, new DealDamageEffect(6, 9));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature());
        }

        [Test]
        public void ChangingAnEffectTargetChangesTheSignature()
        {
            CardDefinition before = data.Card("odd", 1,
                new DealDamageEffect(6, 9, EffectTargeting.Opponent));
            CardDefinition after = data.Card("odd", 1,
                new DealDamageEffect(6, 9, EffectTargeting.Self));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature(),
                "Targeting is a per-subclass field, so the subclass must add it to its signature.");
        }

        [Test]
        public void ChangingAStatusChangesTheSignature()
        {
            CardDefinition before = data.Card("hex", 1, CardType.Skill,
                new ApplyStatusEffect(StatusType.Kindled, 2, 3, EffectTargeting.Self));
            CardDefinition after = data.Card("hex", 1, CardType.Skill,
                new ApplyStatusEffect(StatusType.Exposed, 2, 3, EffectTargeting.Self));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature());
        }

        [Test]
        public void ChangingTheEffectInsideARepeatChangesTheSignature()
        {
            CardDefinition before = data.Card("twin", 1, new RepeatEffect(2, 3, new DealDamageEffect(4, 4)));
            CardDefinition after = data.Card("twin", 1, new RepeatEffect(2, 3, new DealDamageEffect(5, 5)));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature(),
                "The fingerprint has to recurse, or a nested change would go unnoticed.");
        }

        [Test]
        public void SwappingEffectTypeChangesTheSignature()
        {
            CardDefinition before = data.Card("thing", 1, new DealDamageEffect(5, 5));
            CardDefinition after = data.Card("thing", 1, new GainWardEffect(5, 5));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature());
        }

        [Test]
        public void AddingAnEffectChangesTheSignature()
        {
            CardDefinition before = data.Card("thing", 1, new DealDamageEffect(5, 5));
            CardDefinition after = data.Card("thing", 1,
                new DealDamageEffect(5, 5), new GainWardEffect(2, 2));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature());
        }

        [Test]
        public void EnemySignaturesCoverTheirActionPatterns()
        {
            var before = data.Enemy("shade", 20, data.Attack(7), data.Defend(5));
            var after = data.Enemy("shade", 20, data.Attack(8), data.Defend(5));

            Assert.AreNotEqual(before.ContentSignature(), after.ContentSignature());
        }

        [Test]
        public void EnemySignaturesAreStableForIdenticalPatterns()
        {
            var first = data.Enemy("shade", 20, data.Attack(7), data.Defend(5));
            var second = data.Enemy("shade", 20, data.Attack(7), data.Defend(5));

            Assert.AreEqual(first.ContentSignature(), second.ContentSignature());
        }
    }
}
