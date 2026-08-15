using System.Collections.Generic;
using System.Text;
using LastLight.Gameplay.Effects;
using UnityEngine;

namespace LastLight.Gameplay.Cards
{
    /// <summary>
    /// The immutable, authored description of a card - shared by every copy of that card
    /// in every run. This asset is never written to at runtime; per-copy state such as
    /// "this particular copy has been upgraded" lives on <see cref="RuntimeCard"/>.
    /// </summary>
    /// <remarks>
    /// Behaviour is a list of composable <see cref="CardEffect"/> atoms rather than a
    /// card-specific script or an enum consumed by a switch statement. Adding a new card
    /// is data entry; adding a new *kind* of behaviour is one new small effect class.
    /// </remarks>
    [CreateAssetMenu(fileName = "Card_", menuName = "Last Light/Card Definition")]
    public sealed class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CardType cardType = CardType.Attack;

        [Header("Rules")]
        [SerializeField, Min(0)] private int cost = 1;
        [SerializeField, Tooltip("Whether a Shrine is allowed to upgrade copies of this card.")]
        private bool upgradable = true;

        /// <remarks>
        /// [SerializeReference] is what makes polymorphic effects possible: Unity stores the
        /// concrete type alongside the data, so one card asset can hold a DealDamage and an
        /// ApplyStatus side by side. A plain [SerializeField] list would slice them all down
        /// to the base class.
        /// </remarks>
        [SerializeReference] private List<CardEffect> effects = new List<CardEffect>();

        [Header("Presentation")]
        [SerializeField] private Sprite artwork;
        [SerializeField, TextArea(2, 3)] private string flavorText;

        public string Id => id;
        public string DisplayName => displayName;
        public CardType CardType => cardType;
        public int Cost => cost;
        public bool Upgradable => upgradable;
        public Sprite Artwork => artwork;
        public string FlavorText => flavorText;
        public IReadOnlyList<CardEffect> Effects => effects;

        /// <summary>
        /// Builds the card's rules text from its own effects.
        /// </summary>
        /// <remarks>
        /// Descriptions are generated rather than hand-written so that the text on the card
        /// and the behaviour it performs cannot drift apart - changing an effect's numbers
        /// changes the printed card in the same edit.
        /// </remarks>
        public string BuildDescription(bool upgraded)
        {
            var builder = new StringBuilder();

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null) continue;
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(effects[i].Describe(upgraded));
            }

            return builder.ToString();
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        /// <summary>
        /// Authoring hook used by the editor generators and by unit tests. Compiled out of
        /// player builds so that nothing at runtime can write to a definition asset.
        /// </summary>
        public void Configure(string cardId, string cardDisplayName, int cardCost, CardType type,
            IEnumerable<CardEffect> cardEffects, bool canUpgrade = true, string flavor = "")
        {
            id = cardId;
            displayName = cardDisplayName;
            cost = cardCost;
            cardType = type;
            upgradable = canUpgrade;
            flavorText = flavor;
            effects = cardEffects == null ? new List<CardEffect>() : new List<CardEffect>(cardEffects);
        }

        public void SetArtwork(Sprite sprite) => artwork = sprite;

        /// <summary>This asset's authored content as a comparable string.</summary>
        public string ContentSignature() =>
            BuildSignature(id, displayName, cost, cardType, effects, upgradable, flavorText);

        /// <summary>
        /// The same fingerprint computed from loose values, so a generator can ask "would
        /// writing this change anything?" without touching the asset.
        /// </summary>
        /// <remarks>
        /// This exists because [SerializeReference] mints fresh reference ids every time the
        /// effect list is replaced. Rewriting an unchanged asset therefore produces a diff of
        /// pure id churn, which makes real changes hard to spot in review.
        /// </remarks>
        public static string BuildSignature(string cardId, string cardDisplayName, int cardCost, CardType type,
            IEnumerable<CardEffect> cardEffects, bool canUpgrade, string flavor)
        {
            var builder = new StringBuilder();
            builder.Append(cardId).Append('|').Append(cardDisplayName).Append('|').Append(cardCost)
                .Append('|').Append(type).Append('|').Append(canUpgrade).Append('|').Append(flavor);

            if (cardEffects != null)
            {
                foreach (CardEffect effect in cardEffects)
                {
                    builder.Append('|').Append(effect == null ? "null" : effect.Signature());
                }
            }

            return builder.ToString();
        }
#endif
    }
}
