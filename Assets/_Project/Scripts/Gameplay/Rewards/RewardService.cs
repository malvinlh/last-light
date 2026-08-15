using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Common;

namespace LastLight.Gameplay.Rewards
{
    /// <summary>
    /// Rolls the card choices offered after a victory.
    /// </summary>
    /// <remarks>
    /// A pure function over (pool, count, rng) rather than a method on the run controller, so
    /// it can be tested on its own and so the "no duplicate options in one draft" rule lives
    /// somewhere obvious. Shuffle-and-take is used instead of repeated random picks because it
    /// gives distinct results without a rejection loop.
    /// </remarks>
    public static class RewardService
    {
        public static IReadOnlyList<CardDefinition> Roll(IReadOnlyList<CardDefinition> pool, int count, GameRng rng)
        {
            var candidates = new List<CardDefinition>();
            if (pool == null || count <= 0 || rng == null) return candidates;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null) candidates.Add(pool[i]);
            }

            rng.Shuffle(candidates);

            // A pool smaller than the draft size offers everything it has rather than padding.
            if (candidates.Count > count) candidates.RemoveRange(count, candidates.Count - count);

            return candidates;
        }
    }
}
