using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoBrute.Domain;
using NoBrute.Exceptions;
using NoBrute.Internal;
using NoBrute.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NoBrute.Data
{
    /// <summary>
    /// NoBrute Service to handle brute force protection.
    /// </summary>
    public class NoBrute : INoBrute
    {
        private readonly bool enabled;
        private readonly int greenRetries;
        private readonly int increaseRequestTimeMs;
        private readonly int maxIncreaseRequestTimeMs;
        private readonly int timeUntilReset;
        private readonly int blockedStatusCode;
        private readonly TimeUntilResetUnit timeUntilResetUnit;
        private readonly ILogger<NoBrute> logger;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IMemoryCache cache;
        private readonly IDistributedCache distributed;
        private readonly int[] statusCodesForAutoProcess;
        private readonly INoBruteClientIpResolver clientIpResolver;
        private readonly INoBruteEntryLimiter entryLimiter;

        public NoBrute(IServiceProvider provider)
        {
            logger = provider.GetService<ILogger<NoBrute>>();
            httpContextAccessor = provider.GetService<IHttpContextAccessor>();
            cache = provider.GetService<IMemoryCache>();
            distributed = provider.GetService<IDistributedCache>();
            IConfiguration config = provider.GetService<IConfiguration>();

            if (cache == null && distributed == null)
                throw new NoBruteDependencyException("NoBrute requires MemoryCache or IDistributedCache. Add 'services.AddMemoryCache();' in ConfigureServices.");

            if (config == null)
                throw new NoBruteDependencyException("IConfiguration not found. Ensure it is registered.");

            NoBruteRegistrationOptions registration = provider.GetService<NoBruteRegistrationOptions>();

            IConfigurationSection section = config.GetSection("NoBrute");
            enabled = ConfigReader.Read(section, "Enabled", true);
            greenRetries = ConfigReader.Read(section, "GreenRetries", 10);
            increaseRequestTimeMs = ConfigReader.Read(section, "IncreaseRequestTime", 20);
            maxIncreaseRequestTimeMs = registration?.MaxIncreaseRequestTime
                ?? ConfigReader.Read(section, "MaxIncreaseRequestTime", 0);
            timeUntilReset = ConfigReader.Read(section, "TimeUntilReset", 2);
            timeUntilResetUnit = GetUnit(ConfigReader.Read(section, "TimeUntilResetUnit", 'H'));
            blockedStatusCode = registration?.BlockedStatusCode
                ?? ConfigReader.Read(section, "BlockedStatusCode", StatusCodes.Status429TooManyRequests);
            statusCodesForAutoProcess = ReadStatusCodes(section);

            clientIpResolver = provider.GetService<INoBruteClientIpResolver>()
                ?? new NoBruteClientIpResolver(provider, registration?.ClientIp);
            entryLimiter = provider.GetService<INoBruteEntryLimiter>();

            ValidateConfig(section);
        }

        /// <inheritdoc />
        public NoBruteRequestCheck CheckRequest(string requestName = null)
        {
            if (!enabled) return null;

            RequestScope scope = CreateScope(requestName);
            if (scope == null) return null;

            NoBruteEntry entry = GetEntry(scope.CacheKey);
            Evaluation evaluation = Evaluate(entry, scope);

            if (evaluation.Persist)
            {
                SetEntry(scope.CacheKey, evaluation.Entry);
            }

            return evaluation.Check;
        }

        /// <inheritdoc />
        public async Task<NoBruteRequestCheck> CheckRequestAsync(string requestName = null, CancellationToken cancellationToken = default)
        {
            if (!enabled) return null;

            RequestScope scope = CreateScope(requestName);
            if (scope == null) return null;

            NoBruteEntry entry = await GetEntryAsync(scope.CacheKey, cancellationToken).ConfigureAwait(false);
            Evaluation evaluation = Evaluate(entry, scope);

            if (evaluation.Persist)
            {
                await SetEntryAsync(scope.CacheKey, evaluation.Entry, cancellationToken).ConfigureAwait(false);
            }

            return evaluation.Check;
        }

        /// <inheritdoc />
        public bool ReleaseRequest(string requestName = null)
        {
            RequestScope scope = CreateScope(requestName);
            if (scope == null) return true;

            NoBruteEntry entry = GetEntry(scope.CacheKey);
            if (entry == null) return true;

            if (!TryRemoveRequestItem(entry, scope, out bool entryIsEmpty)) return false;

            if (entryIsEmpty)
            {
                RemoveEntry(scope.CacheKey);
            }
            else
            {
                SetEntry(scope.CacheKey, entry);
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> ReleaseRequestAsync(string requestName = null, CancellationToken cancellationToken = default)
        {
            RequestScope scope = CreateScope(requestName);
            if (scope == null) return true;

            NoBruteEntry entry = await GetEntryAsync(scope.CacheKey, cancellationToken).ConfigureAwait(false);
            if (entry == null) return true;

            if (!TryRemoveRequestItem(entry, scope, out bool entryIsEmpty)) return false;

            if (entryIsEmpty)
            {
                await RemoveEntryAsync(scope.CacheKey, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SetEntryAsync(scope.CacheKey, entry, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        /// <inheritdoc />
        public bool AutoProcessRequestRelease(int status, string requestName = null)
        {
            if (!ShouldAutoProcess(status)) return false;

            ReleaseRequest(requestName);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> AutoProcessRequestReleaseAsync(int status, string requestName = null, CancellationToken cancellationToken = default)
        {
            if (!ShouldAutoProcess(status)) return false;

            await ReleaseRequestAsync(requestName, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private bool ShouldAutoProcess(int status) => statusCodesForAutoProcess.Contains(status);

        /// <summary>
        /// Collects everything that is needed to look at the current request. Returns <c>null</c>
        /// when there is no HTTP context (e.g. when called from a background job).
        /// </summary>
        private RequestScope CreateScope(string requestName)
        {
            HttpContext context = httpContextAccessor?.HttpContext;

            if (context == null)
            {
                logger?.LogDebug("NoBrute was called outside of an HTTP request and did nothing.");
                return null;
            }

            HttpRequest request = context.Request;
            string ip = clientIpResolver.ResolveClientIp(context);

            return new RequestScope
            {
                Request = request,
                Name = requestName ?? request.Path.ToString(),
                Ip = ip,
                CacheKey = GenerateCacheKey(ip)
            };
        }

        /// <summary>
        /// Applies the hit counting rules to the (possibly missing) cache entry.
        /// </summary>
        private Evaluation Evaluate(NoBruteEntry entry, RequestScope scope)
        {
            TimeSpan expireIn = GetExpireTimespan();

            // Circuit breaker: once the configured number of tracked clients is reached, unknown clients
            // are rejected immediately instead of allocating yet another cache entry.
            if (entryLimiter != null && !entryLimiter.TryTrack(scope.CacheKey, expireIn))
            {
                logger?.LogWarning(
                    "NoBrute hard blocked {RemoteAddr} for {RequestName}: the tracked entry limit of {MaxTrackedEntries} is reached.",
                    scope.Ip,
                    scope.Name,
                    entryLimiter.MaxTrackedEntries);

                return new Evaluation
                {
                    Check = new NoBruteRequestCheck
                    {
                        IsGreenRequest = false,
                        IsBlocked = true,
                        BlockedStatusCode = blockedStatusCode,
                        AppendRequestTime = 0,
                        RemoteAddr = scope.Ip,
                        RequestNum = 0,
                        ResetTime = DateTime.Now + expireIn
                    },
                    Entry = null,
                    Persist = false
                };
            }

            if (entry == null)
            {
                entry = new NoBruteEntry { IP = scope.Ip, Requests = new List<NoBruteRequestItem>() };
            }
            else if (entry.Requests == null)
            {
                entry.Requests = new List<NoBruteRequestItem>();
            }

            ClearExpiredItems(entry, expireIn);
            NoBruteRequestItem requestItem = ManageRequestEntry(entry, scope.Name, scope.Request);

            return new Evaluation
            {
                Check = new NoBruteRequestCheck
                {
                    IsGreenRequest = requestItem.Hitcount <= greenRetries,
                    IsBlocked = false,
                    BlockedStatusCode = blockedStatusCode,
                    RemoteAddr = scope.Ip,
                    RequestNum = requestItem.Hitcount,
                    ResetTime = requestItem.LastHit + expireIn,
                    AppendRequestTime = CalculateAppendRequestTime(requestItem.Hitcount)
                },
                Entry = entry,
                Persist = true
            };
        }

        /// <summary>
        /// Calculates the delay for the given hit count, capped by <c>MaxIncreaseRequestTime</c>.
        /// </summary>
        private int CalculateAppendRequestTime(int hitcount)
        {
            long appendTime = Math.Max(0L, (long)(hitcount - greenRetries) * increaseRequestTimeMs);

            if (maxIncreaseRequestTimeMs > 0 && appendTime > maxIncreaseRequestTimeMs)
            {
                appendTime = maxIncreaseRequestTimeMs;
            }

            return appendTime > int.MaxValue ? int.MaxValue : (int)appendTime;
        }

        /// <summary>
        /// Removes the request item of the current scope from the entry.
        /// </summary>
        /// <param name="entryIsEmpty">Set to <c>true</c> when the entry no longer holds any request.</param>
        /// <returns><c>false</c> when there was nothing to remove.</returns>
        private bool TryRemoveRequestItem(NoBruteEntry entry, RequestScope scope, out bool entryIsEmpty)
        {
            entryIsEmpty = false;

            if (entry?.Requests == null) return false;

            NoBruteRequestItem requestItem = entry.Requests
                .FirstOrDefault(x => x.RequestName == scope.Name && x.RequestMethod == scope.Request.Method);

            if (requestItem == null) return false;

            entry.Requests.Remove(requestItem);
            entryIsEmpty = entry.Requests.Count == 0;

            return true;
        }

        private NoBruteEntry GetEntry(string cacheKey)
        {
            if (distributed == null)
            {
                cache.TryGetValue(cacheKey, out NoBruteEntry item);
                return item;
            }

            return Deserialize(distributed.Get(cacheKey));
        }

        private async Task<NoBruteEntry> GetEntryAsync(string cacheKey, CancellationToken cancellationToken)
        {
            if (distributed == null)
            {
                cache.TryGetValue(cacheKey, out NoBruteEntry item);
                return item;
            }

            return Deserialize(await distributed.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false));
        }

        private void SetEntry(string cacheKey, NoBruteEntry item)
        {
            if (item == null) return;

            if (distributed == null)
            {
                cache.Set(cacheKey, item, CreateMemoryCacheOptions());
            }
            else
            {
                distributed.Set(cacheKey, Serialize(item), CreateDistributedCacheOptions());
            }
        }

        private async Task SetEntryAsync(string cacheKey, NoBruteEntry item, CancellationToken cancellationToken)
        {
            if (item == null) return;

            if (distributed == null)
            {
                cache.Set(cacheKey, item, CreateMemoryCacheOptions());
                return;
            }

            await distributed
                .SetAsync(cacheKey, Serialize(item), CreateDistributedCacheOptions(), cancellationToken)
                .ConfigureAwait(false);
        }

        private void RemoveEntry(string cacheKey)
        {
            if (distributed == null)
            {
                cache.Remove(cacheKey);
            }
            else
            {
                distributed.Remove(cacheKey);
            }

            entryLimiter?.Release(cacheKey);
        }

        private async Task RemoveEntryAsync(string cacheKey, CancellationToken cancellationToken)
        {
            if (distributed == null)
            {
                cache.Remove(cacheKey);
            }
            else
            {
                await distributed.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            }

            entryLimiter?.Release(cacheKey);
        }

        /// <summary>
        /// Cache entries always expire: without an expiration a bot net using many source addresses
        /// would grow the cache until the process (or Redis) runs out of memory.
        /// </summary>
        private MemoryCacheEntryOptions CreateMemoryCacheOptions()
        {
            TimeSpan expireIn = GetExpireTimespan();
            MemoryCacheEntryOptions options = new MemoryCacheEntryOptions();

            if (expireIn > TimeSpan.Zero)
            {
                options.AbsoluteExpirationRelativeToNow = expireIn;
            }

            return options;
        }

        private DistributedCacheEntryOptions CreateDistributedCacheOptions()
        {
            TimeSpan expireIn = GetExpireTimespan();
            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions();

            if (expireIn > TimeSpan.Zero)
            {
                options.AbsoluteExpirationRelativeToNow = expireIn;
            }

            return options;
        }

        private void ClearExpiredItems(NoBruteEntry entry, TimeSpan expireIn)
        {
            entry.Requests = entry.Requests.Where(x => !x.IsExpired(expireIn)).ToList();
        }

        private TimeSpan GetExpireTimespan()
        {
            return timeUntilResetUnit switch
            {
                TimeUntilResetUnit.Years => TimeSpan.FromDays(timeUntilReset * 365),
                TimeUntilResetUnit.Months => TimeSpan.FromDays(timeUntilReset * 30),
                TimeUntilResetUnit.Days => TimeSpan.FromDays(timeUntilReset),
                TimeUntilResetUnit.Hours => TimeSpan.FromHours(timeUntilReset),
                TimeUntilResetUnit.Minutes => TimeSpan.FromMinutes(timeUntilReset),
                TimeUntilResetUnit.Seconds => TimeSpan.FromSeconds(timeUntilReset),
                TimeUntilResetUnit.Miliseconds => TimeSpan.FromMilliseconds(timeUntilReset),
                _ => TimeSpan.Zero
            };
        }

        private NoBruteRequestItem ManageRequestEntry(NoBruteEntry entry, string requestName, HttpRequest data)
        {
            var requestItem = entry.Requests.FirstOrDefault(x => x.RequestName == requestName && x.RequestMethod == data.Method);
            if (requestItem != null)
            {
                requestItem.Hitcount++;
                requestItem.LastHit = DateTime.Now;
            }
            else
            {
                requestItem = new NoBruteRequestItem
                {
                    Hitcount = 1,
                    RequestMethod = data.Method,
                    RequestName = requestName,
                    RequestPath = data.Path,
                    RequestQuery = data.QueryString.HasValue ? data.QueryString.ToString() : null,
                    LastHit = DateTime.Now
                };
                entry.Requests.Add(requestItem);
            }
            return requestItem;
        }

        private string GenerateCacheKey(string ip)
        {
            return Convert.ToHexStringLower(SHA512.HashData(Encoding.UTF8.GetBytes(ip ?? string.Empty)));
        }

        /// <summary>
        /// Maps the configured character to its unit. The enum members are defined by their character
        /// ('y', 'd', 'M', 'H', 'i', 's', 'n'), so the value has to be compared numerically:
        /// parsing by name would never match and silently fall back to hours.
        /// </summary>
        private TimeUntilResetUnit GetUnit(char value)
        {
            return Enum.IsDefined(typeof(TimeUntilResetUnit), (int)value)
                ? (TimeUntilResetUnit)value
                : TimeUntilResetUnit.Hours;
        }

        private static int[] ReadStatusCodes(IConfigurationSection section)
        {
            IConfigurationSection statusCodes = section?.GetSection("StatusCodesForAutoProcess");

            if (statusCodes == null)
            {
                return new[] { StatusCodes.Status200OK };
            }

            return statusCodes
                .AsEnumerable()
                .Select(x => int.TryParse(x.Value, out var val) ? val : StatusCodes.Status200OK)
                .ToArray();
        }

        private void ValidateConfig(IConfigurationSection section)
        {
            ValidateConfigEntry(section, "Enabled", typeof(bool), enabled);
            ValidateConfigEntry(section, "GreenRetries", typeof(int), greenRetries);
            ValidateConfigEntry(section, "IncreaseRequestTime", typeof(int), increaseRequestTimeMs);
            ValidateConfigEntry(section, "TimeUntilReset", typeof(int), timeUntilReset);
            ValidateConfigEntry(section, "TimeUntilResetUnit", typeof(char), timeUntilResetUnit);
        }

        private void ValidateConfigEntry(IConfigurationSection section, string name, Type expectedType, object value)
        {
            if (value == null)
                throw new NoBruteConfigurationException($"Invalid value for configuration NoBrute->{name}. Expected type: {expectedType.FullName}");
        }

        private static byte[] Serialize<T>(T obj) => Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(obj));

        private static NoBruteEntry Deserialize(byte[] data)
        {
            return data == null || data.Length == 0
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<NoBruteEntry>(Encoding.UTF8.GetString(data));
        }

        /// <summary>
        /// Everything the checks need to know about the current request.
        /// </summary>
        private sealed class RequestScope
        {
            public HttpRequest Request { get; set; }

            public string Name { get; set; }

            public string Ip { get; set; }

            public string CacheKey { get; set; }
        }

        /// <summary>
        /// Result of applying the hit counting rules, shared by the sync and async code paths.
        /// </summary>
        private sealed class Evaluation
        {
            public NoBruteRequestCheck Check { get; set; }

            public NoBruteEntry Entry { get; set; }

            public bool Persist { get; set; }
        }
    }
}
