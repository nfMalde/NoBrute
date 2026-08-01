using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoBrute.Domain;
using NoBrute.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NoBrute.Data
{
    /// <summary>
    /// Default <see cref="INoBruteEntryLimiter"/>.
    /// Keeps a bounded, self pruning set of tracked cache keys per application instance.
    /// </summary>
    public sealed class NoBruteEntryLimiter : INoBruteEntryLimiter
    {
        private static readonly TimeSpan FallbackLifetime = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MinimumPruneInterval = TimeSpan.FromSeconds(1);

        private readonly ConcurrentDictionary<string, long> trackedEntries = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        private readonly object syncRoot = new object();
        private readonly int maxTrackedEntries;
        private readonly ILogger<NoBruteEntryLimiter> logger;
        private long nextPruneTicks;
        private bool limitReportedAsReached;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteEntryLimiter"/> class using the
        /// <c>NoBrute:MaxTrackedEntries</c> configuration value.
        /// </summary>
        /// <param name="provider">The service provider.</param>
        public NoBruteEntryLimiter(IServiceProvider provider)
            : this(provider, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteEntryLimiter"/> class.
        /// </summary>
        /// <param name="provider">The service provider.</param>
        /// <param name="overrideMaxTrackedEntries">Limit set in code. If <c>null</c>, configuration is used.</param>
        public NoBruteEntryLimiter(IServiceProvider provider, int? overrideMaxTrackedEntries)
        {
            this.logger = provider?.GetService<ILogger<NoBruteEntryLimiter>>();

            IConfiguration configuration = provider?.GetService<IConfiguration>();
            int configured = overrideMaxTrackedEntries
                ?? ConfigReader.Read(configuration?.GetSection("NoBrute"), "MaxTrackedEntries", 0);

            this.maxTrackedEntries = configured < 0 ? 0 : configured;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteEntryLimiter"/> class with an explicit limit.
        /// </summary>
        /// <param name="maxTrackedEntries">The maximum number of tracked entries. <c>0</c> means unlimited.</param>
        public NoBruteEntryLimiter(int maxTrackedEntries)
        {
            this.maxTrackedEntries = maxTrackedEntries < 0 ? 0 : maxTrackedEntries;
        }

        /// <inheritdoc />
        public int MaxTrackedEntries => this.maxTrackedEntries;

        /// <inheritdoc />
        public int TrackedEntries => this.trackedEntries.Count;

        /// <inheritdoc />
        public bool IsLimitReached => this.maxTrackedEntries > 0 && this.trackedEntries.Count >= this.maxTrackedEntries;

        /// <inheritdoc />
        public bool TryTrack(string cacheKey, TimeSpan lifetime)
        {
            // Unlimited mode keeps no state at all, so the limiter itself can never become the leak.
            if (this.maxTrackedEntries <= 0 || string.IsNullOrEmpty(cacheKey))
            {
                return true;
            }

            long expiresAt = DateTimeOffset.UtcNow
                .Add(lifetime > TimeSpan.Zero ? lifetime : FallbackLifetime)
                .UtcTicks;

            if (this.trackedEntries.ContainsKey(cacheKey))
            {
                this.trackedEntries[cacheKey] = expiresAt;
                return true;
            }

            lock (this.syncRoot)
            {
                if (this.trackedEntries.ContainsKey(cacheKey))
                {
                    this.trackedEntries[cacheKey] = expiresAt;
                    return true;
                }

                if (this.trackedEntries.Count >= this.maxTrackedEntries)
                {
                    this.Prune();

                    if (this.trackedEntries.Count >= this.maxTrackedEntries)
                    {
                        this.ReportLimitReached();
                        return false;
                    }
                }

                this.trackedEntries[cacheKey] = expiresAt;
                this.limitReportedAsReached = false;

                return true;
            }
        }

        /// <inheritdoc />
        public void Release(string cacheKey)
        {
            if (!string.IsNullOrEmpty(cacheKey))
            {
                this.trackedEntries.TryRemove(cacheKey, out _);
            }
        }

        /// <summary>
        /// Removes expired entries. Called while holding <see cref="syncRoot"/> and throttled,
        /// so a flood of unknown clients cannot turn this into an O(n) scan per request.
        /// </summary>
        private void Prune()
        {
            long now = DateTimeOffset.UtcNow.UtcTicks;

            if (now < this.nextPruneTicks)
            {
                return;
            }

            this.nextPruneTicks = now + MinimumPruneInterval.Ticks;

            foreach (KeyValuePair<string, long> entry in this.trackedEntries)
            {
                if (entry.Value <= now)
                {
                    this.trackedEntries.TryRemove(entry.Key, out _);
                }
            }
        }

        private void ReportLimitReached()
        {
            if (this.limitReportedAsReached)
            {
                return;
            }

            this.limitReportedAsReached = true;

            this.logger?.LogWarning(
                "NoBrute reached its limit of {MaxTrackedEntries} tracked clients. New clients are hard blocked until tracked entries expire.",
                this.maxTrackedEntries);
        }
    }
}
