using System;

namespace NoBrute.Domain
{
    /// <summary>
    /// Global circuit breaker that caps how many distinct clients NoBrute keeps in the cache.
    /// Protects the memory cache / Redis instance against being flooded by a botnet using
    /// millions of distinct source addresses.
    /// </summary>
    public interface INoBruteEntryLimiter
    {
        /// <summary>
        /// Gets the configured maximum number of tracked entries. <c>0</c> means unlimited.
        /// </summary>
        int MaxTrackedEntries { get; }

        /// <summary>
        /// Gets the number of currently tracked entries. Always <c>0</c> when unlimited.
        /// </summary>
        int TrackedEntries { get; }

        /// <summary>
        /// Gets a value indicating whether the limit is currently reached and new clients are hard blocked.
        /// </summary>
        bool IsLimitReached { get; }

        /// <summary>
        /// Tries to reserve a slot for the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key of the entry.</param>
        /// <param name="lifetime">How long the entry stays in the cache.</param>
        /// <returns>
        /// <c>true</c> if the key is already tracked or a slot was available,
        /// <c>false</c> if the limit is reached and the request must be hard blocked.
        /// </returns>
        bool TryTrack(string cacheKey, TimeSpan lifetime);

        /// <summary>
        /// Releases the slot of the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key of the entry.</param>
        void Release(string cacheKey);
    }
}
