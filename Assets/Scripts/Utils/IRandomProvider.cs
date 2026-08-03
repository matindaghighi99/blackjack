using System;

namespace BlackjackGame.Utils
{
    /// <summary>
    /// Abstraction over randomness so shuffling can be seeded deterministically for
    /// tests, or swapped for a server-authoritative RNG to make shuffles verifiable.
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>Returns a non-negative random integer less than <paramref name="maxExclusive"/>.</summary>
        int Next(int maxExclusive);
    }

    /// <summary>Default provider backed by <see cref="System.Random"/>.</summary>
    public sealed class SystemRandomProvider : IRandomProvider
    {
        private readonly Random _random;

        public SystemRandomProvider(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
