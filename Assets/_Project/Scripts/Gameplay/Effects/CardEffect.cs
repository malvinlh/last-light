using System;
using UnityEngine;

namespace LastLight.Gameplay.Effects
{
    /// <summary>
    /// One atomic thing a card (or an enemy action) can do.
    /// </summary>
    /// <remarks>
    /// Every effect carries both its normal and its upgraded magnitude. That is what lets a
    /// Shrine upgrade change a card's numbers without duplicating the asset or mutating it:
    /// the copy being played simply asks for the upgraded value instead.
    ///
    /// Subclasses are serialized by reference from <see cref="Cards.CardDefinition"/>, so
    /// each one needs a parameterless constructor for Unity's deserializer alongside the
    /// convenience constructor the editor generators use.
    /// </remarks>
    [Serializable]
    public abstract class CardEffect
    {
        [SerializeField, Min(0)] private int amount;
        [SerializeField, Min(0), Tooltip("Magnitude used when the played copy is upgraded.")]
        private int upgradedAmount;

        protected CardEffect() { }

        protected CardEffect(int amount, int upgradedAmount)
        {
            this.amount = amount;
            this.upgradedAmount = upgradedAmount;
        }

        public int Amount => amount;
        public int UpgradedAmount => upgradedAmount;

        public int AmountFor(bool upgraded) => upgraded ? upgradedAmount : amount;

        /// <summary>Applies this effect. Everything it is allowed to touch comes from the context.</summary>
        public abstract void Resolve(EffectContext context);

        /// <summary>One sentence of player-facing rules text for this effect.</summary>
        public abstract string Describe(bool upgraded);
    }
}
