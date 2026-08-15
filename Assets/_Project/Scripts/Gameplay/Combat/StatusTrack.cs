using System;
using System.Collections.Generic;

namespace LastLight.Gameplay.Combat
{
    /// <summary>Stack counts for the statuses currently on one combatant.</summary>
    public sealed class StatusTrack
    {
        private readonly Dictionary<StatusType, int> stacks = new Dictionary<StatusType, int>();
        private readonly List<StatusType> tickBuffer = new List<StatusType>();

        public int Get(StatusType status) => stacks.TryGetValue(status, out int value) ? value : 0;

        public bool Has(StatusType status) => Get(status) > 0;

        public void Add(StatusType status, int amount)
        {
            if (amount <= 0) return;
            stacks[status] = Get(status) + amount;
        }

        public void Clear() => stacks.Clear();

        /// <summary>Statuses with at least one stack, for the UI to render as pips.</summary>
        public IEnumerable<KeyValuePair<StatusType, int>> Active
        {
            get
            {
                foreach (var pair in stacks)
                {
                    if (pair.Value > 0) yield return pair;
                }
            }
        }

        /// <summary>
        /// Sheds one stack of every decaying status. Called at the start of the owner's turn,
        /// so an Exposed applied on your turn is still fully in effect when the enemy is hit.
        /// </summary>
        public void TickAtOwnerTurnStart()
        {
            tickBuffer.Clear();
            foreach (var pair in stacks)
            {
                if (pair.Value > 0 && StatusInfo.Decays(pair.Key)) tickBuffer.Add(pair.Key);
            }

            for (int i = 0; i < tickBuffer.Count; i++)
            {
                stacks[tickBuffer[i]] = Math.Max(0, stacks[tickBuffer[i]] - 1);
            }
        }
    }
}
