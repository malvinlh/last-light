using System;
using UnityEngine;

namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// The tuning knobs for combat, authored on the run config rather than hard-coded so the
    /// numbers can be balanced without touching code.
    /// </summary>
    [Serializable]
    public sealed class CombatRules
    {
        [SerializeField, Min(1), Tooltip("Cards drawn at the start of each player turn.")]
        private int handSize = 5;

        [SerializeField, Min(0), Tooltip("Focus the player gets back each turn.")]
        private int focusPerTurn = 3;

        public CombatRules() { }

        public CombatRules(int handSize, int focusPerTurn)
        {
            this.handSize = handSize;
            this.focusPerTurn = focusPerTurn;
        }

        public int HandSize => handSize;
        public int FocusPerTurn => focusPerTurn;
    }
}
