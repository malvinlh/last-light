using System;
using System.Collections.Generic;
using LastLight.Gameplay.Effects;
using UnityEngine;

namespace LastLight.Gameplay.Enemies
{
    /// <summary>
    /// One step in an enemy's looping pattern.
    /// </summary>
    /// <remarks>
    /// Enemy actions reuse the exact same <see cref="CardEffect"/> atoms as cards. That is the
    /// main payoff of pushing behaviour into data: an enemy that gains Ward and a card that
    /// gains Ward run identical code, so there is only ever one implementation to get right.
    /// </remarks>
    [Serializable]
    public sealed class EnemyAction
    {
        [SerializeField] private string label;
        [SerializeField] private IntentKind intent = IntentKind.Attack;
        [SerializeReference] private List<CardEffect> effects = new List<CardEffect>();

        public EnemyAction() { }

        public EnemyAction(string label, IntentKind intent, IEnumerable<CardEffect> effects)
        {
            this.label = label;
            this.intent = intent;
            this.effects = effects == null ? new List<CardEffect>() : new List<CardEffect>(effects);
        }

        public string Label => label;
        public IntentKind Intent => intent;
        public IReadOnlyList<CardEffect> Effects => effects;

        /// <summary>
        /// The unmodified number shown on the intent telegraph, read from the first effect that
        /// carries one. Deriving it from the effects means a designer cannot change an action's
        /// damage and forget to update the number the player is shown.
        /// </summary>
        public int BaseIntentValue()
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is DealDamageEffect || effects[i] is GainWardEffect) return effects[i].Amount;
            }

            return 0;
        }

        /// <summary>Whether this action's telegraphed number should be run through the damage pipeline.</summary>
        public bool IsAttack => intent == IntentKind.Attack;

        /// <summary>This action's authored content as a comparable string, for the editor generators.</summary>
        public string Signature()
        {
            var builder = new System.Text.StringBuilder();
            builder.Append(label).Append(':').Append(intent);

            for (int i = 0; i < effects.Count; i++)
            {
                builder.Append(',').Append(effects[i] == null ? "null" : effects[i].Signature());
            }

            return builder.ToString();
        }
    }
}
