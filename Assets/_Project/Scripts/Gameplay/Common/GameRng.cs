using System;
using System.Collections.Generic;

namespace LastLight.Gameplay.Common
{
    /// <summary>
    /// The single source of randomness for a run.
    /// </summary>
    /// <remarks>
    /// Nothing in the gameplay layer calls <c>UnityEngine.Random</c>. Every system that
    /// needs randomness is handed one of these instead, which buys two things:
    /// a whole run is reproducible from one seed, and every unit test is deterministic
    /// without needing to stub anything out.
    /// </remarks>
    public sealed class GameRng
    {
        private readonly Random random;

        public GameRng(int seed)
        {
            Seed = seed;
            random = new Random(seed);
        }

        public int Seed { get; }

        /// <summary>Returns a value in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);

        /// <summary>
        /// Fisher-Yates shuffle: in place, unbiased, O(n). Walking backwards and swapping
        /// with any index at or below the cursor gives every permutation equal probability,
        /// which the naive "swap with any random index" version does not.
        /// </summary>
        public void Shuffle<T>(IList<T> items)
        {
            if (items == null) return;

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
