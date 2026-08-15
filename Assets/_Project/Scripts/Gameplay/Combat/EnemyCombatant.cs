using System.Collections.Generic;
using LastLight.Gameplay.Enemies;

namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// The enemy's live state for one combat, including where it is in its action pattern.
    /// </summary>
    /// <remarks>
    /// <see cref="CurrentAction"/> is always the action the enemy will take on its *next* turn,
    /// set before the player is ever asked for input. That ordering is what makes the intent
    /// telegraph honest: the player is shown the decision the game has already made.
    /// </remarks>
    public sealed class EnemyCombatant : Combatant
    {
        private readonly IReadOnlyList<EnemyAction> pattern;
        private int actionIndex;

        public EnemyCombatant(EnemyDefinition definition)
            : base(definition != null ? definition.DisplayName : "Enemy",
                   definition != null ? definition.MaxLight : 1,
                   definition != null ? definition.MaxLight : 1)
        {
            Definition = definition;
            pattern = definition != null ? definition.Pattern : new List<EnemyAction>();
        }

        public EnemyDefinition Definition { get; }

        /// <summary>The action this enemy will perform on its next turn, or null if it has no pattern.</summary>
        public EnemyAction CurrentAction =>
            pattern == null || pattern.Count == 0 ? null : pattern[actionIndex % pattern.Count];

        /// <summary>Steps to the next action in the loop. Called after the enemy acts.</summary>
        public void AdvanceAction()
        {
            if (pattern == null || pattern.Count == 0)
            {
                actionIndex = 0;
                return;
            }

            actionIndex = (actionIndex + 1) % pattern.Count;
        }
    }
}
