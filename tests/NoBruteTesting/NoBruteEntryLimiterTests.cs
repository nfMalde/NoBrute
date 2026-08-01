using NoBrute.Data;
using NoBrute.Domain;
using Shouldly;
using System;
using Xunit;

namespace NoBruteTesting
{
    /// <summary>
    /// Tests for the global circuit breaker that caps the number of tracked clients.
    /// </summary>
    public class NoBruteEntryLimiterTests
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// It should not track anything when no limit is configured.
        /// </summary>
        [Fact]
        public void ItShouldNotTrackAnythingWhenUnlimited()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(0);

            for (int i = 0; i < 1000; i++)
            {
                limiter.TryTrack($"key-{i}", Lifetime).ShouldBeTrue();
            }

            limiter.TrackedEntries.ShouldBe(0);
            limiter.IsLimitReached.ShouldBeFalse();
        }

        /// <summary>
        /// It should reject new clients once the limit is reached.
        /// </summary>
        [Fact]
        public void ItShouldRejectNewEntriesWhenTheLimitIsReached()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(3);

            limiter.TryTrack("a", Lifetime).ShouldBeTrue();
            limiter.TryTrack("b", Lifetime).ShouldBeTrue();
            limiter.TryTrack("c", Lifetime).ShouldBeTrue();

            limiter.IsLimitReached.ShouldBeTrue();
            limiter.TryTrack("d", Lifetime).ShouldBeFalse();
            limiter.TrackedEntries.ShouldBe(3);
        }

        /// <summary>
        /// It should keep serving clients that are already tracked, even at the limit.
        /// </summary>
        [Fact]
        public void ItShouldKeepServingKnownEntriesAtTheLimit()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(2);

            limiter.TryTrack("a", Lifetime).ShouldBeTrue();
            limiter.TryTrack("b", Lifetime).ShouldBeTrue();

            limiter.TryTrack("a", Lifetime).ShouldBeTrue();
            limiter.TryTrack("c", Lifetime).ShouldBeFalse();
        }

        /// <summary>
        /// It should free the slot again when an entry is released.
        /// </summary>
        [Fact]
        public void ItShouldFreeSlotsOnRelease()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(1);

            limiter.TryTrack("a", Lifetime).ShouldBeTrue();
            limiter.TryTrack("b", Lifetime).ShouldBeFalse();

            limiter.Release("a");

            limiter.TrackedEntries.ShouldBe(0);
            limiter.TryTrack("b", Lifetime).ShouldBeTrue();
        }

        /// <summary>
        /// It should reuse slots of expired entries.
        /// </summary>
        [Fact]
        public void ItShouldPruneExpiredEntries()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(1);

            limiter.TryTrack("a", TimeSpan.FromMilliseconds(1)).ShouldBeTrue();

            System.Threading.Thread.Sleep(20);

            limiter.TryTrack("b", Lifetime).ShouldBeTrue();
            limiter.TrackedEntries.ShouldBe(1);
        }

        /// <summary>
        /// It should treat a negative limit as unlimited.
        /// </summary>
        [Fact]
        public void ItShouldTreatNegativeLimitsAsUnlimited()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(-1);

            limiter.MaxTrackedEntries.ShouldBe(0);
            limiter.TryTrack("a", Lifetime).ShouldBeTrue();
            limiter.IsLimitReached.ShouldBeFalse();
        }

        /// <summary>
        /// It should never block when there is no key to track.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ItShouldIgnoreEmptyKeys(string cacheKey)
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(1);

            limiter.TryTrack(cacheKey, Lifetime).ShouldBeTrue();
            limiter.TrackedEntries.ShouldBe(0);

            Should.NotThrow(() => limiter.Release(cacheKey));
        }

        /// <summary>
        /// It should use a fallback lifetime instead of expiring entries immediately.
        /// </summary>
        [Fact]
        public void ItShouldUseAFallbackLifetimeForEmptyLifetimes()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(1);

            limiter.TryTrack("a", TimeSpan.Zero).ShouldBeTrue();
            limiter.TryTrack("b", Lifetime).ShouldBeFalse();
        }

        /// <summary>
        /// It should not throw when releasing an entry that was never tracked.
        /// </summary>
        [Fact]
        public void ItShouldIgnoreUnknownReleases()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(1);

            Should.NotThrow(() => limiter.Release("never-tracked"));
            limiter.TrackedEntries.ShouldBe(0);
        }

        /// <summary>
        /// It should refresh the lifetime of clients that keep hitting the application.
        /// </summary>
        [Fact]
        public void ItShouldRefreshTheLifetimeOfKnownEntries()
        {
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(1);

            limiter.TryTrack("a", TimeSpan.FromMilliseconds(30)).ShouldBeTrue();
            limiter.TryTrack("a", Lifetime).ShouldBeTrue();

            System.Threading.Thread.Sleep(50);

            // The refreshed entry is still alive, so the slot is not handed to somebody else.
            limiter.TryTrack("b", Lifetime).ShouldBeFalse();
        }

        /// <summary>
        /// It should stay consistent when many threads race for the last slots.
        /// </summary>
        [Fact]
        public void ItShouldNotExceedTheLimitUnderConcurrency()
        {
            const int limit = 50;
            INoBruteEntryLimiter limiter = new NoBruteEntryLimiter(limit);

            System.Threading.Tasks.Parallel.For(0, 5000, i => limiter.TryTrack($"key-{i}", Lifetime));

            limiter.TrackedEntries.ShouldBe(limit);
        }
    }
}
