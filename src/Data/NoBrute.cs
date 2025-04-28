using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoBrute.Domain;
using NoBrute.Exceptions;
using NoBrute.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
        private readonly int timeUntilReset;
        private readonly TimeUntilResetUnit timeUntilResetUnit;
        private readonly ILogger<NoBrute> logger;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IMemoryCache cache;
        private readonly IDistributedCache distributed;
        private readonly int[] statusCodesForAutoProcess;

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

            IConfigurationSection section = config.GetSection("NoBrute");
            enabled = section.GetValue("Enabled", true);
            greenRetries = section.GetValue("GreenRetries", 10);
            increaseRequestTimeMs = section.GetValue("IncreaseRequestTime", 20);
            timeUntilReset = section.GetValue("TimeUntilReset", 2);
            timeUntilResetUnit = GetUnit(section.GetValue<char>("TimeUntilResetUnit", 'H'));
            statusCodesForAutoProcess = section.GetSection("StatusCodesForAutoProcess")
                .AsEnumerable()
                .Select(x => int.TryParse(x.Value, out var val) ? val : 200)
                .ToArray();

            ValidateConfig(section);
        }

        public NoBruteRequestCheck CheckRequest(string requestName = null)
        {
            if (!enabled) return null;

            HttpRequest request = httpContextAccessor.HttpContext.Request;
            string name = requestName ?? request.Path;
            string ip = httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();
            string cacheKey = GenerateCacheKey(ip);

            if (!TryGetCacheValue(cacheKey, out var noBruteEntry))
            {
                noBruteEntry = new NoBruteEntry { IP = ip, Requests = new List<NoBruteRequestItem>() };
            }

            ClearExpiredItems(noBruteEntry);
            var requestItem = ManageRequestEntry(noBruteEntry, name, request);
            SetCacheItem(cacheKey, noBruteEntry);

            return new NoBruteRequestCheck
            {
                IsGreenRequest = requestItem.Hitcount <= greenRetries,
                RemoteAddr = ip,
                RequestNum = requestItem.Hitcount,
                ResetTime = requestItem.LastHit + GetExpireTimespan(),
                AppendRequestTime = Math.Max(0, (requestItem.Hitcount - greenRetries) * increaseRequestTimeMs)
            };
        }

        public bool ReleaseRequest(string requestName = null)
        {
            HttpRequest request = httpContextAccessor.HttpContext.Request;
            string name = requestName ?? request.Path;
            string ip = httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();
            string cacheKey = GenerateCacheKey(ip);

            if (!TryGetCacheValue(cacheKey, out var entry) || entry == null) return true;

            var requestItem = entry.Requests.FirstOrDefault(x => x.RequestName == name && x.RequestMethod == request.Method);
            if (requestItem != null)
            {
                entry.Requests.Remove(requestItem);
                SetCacheItem(cacheKey, entry);
                return true;
            }

            return false;
        }

        public bool AutoProcessRequestRelease(int status, string requestName = null)
        {
            if (statusCodesForAutoProcess.Contains(status))
            {
                ReleaseRequest(requestName);
                return true;
            }
            return false;
        }

        private bool TryGetCacheValue(string cacheKey, out NoBruteEntry item)
        {
            if (distributed == null)
                return cache.TryGetValue(cacheKey, out item);

            var result = distributed.Get(cacheKey);
            item = result != null ? Deserialize<NoBruteEntry>(result) : null;
            return item != null;
        }

        private void SetCacheItem(string cacheKey, NoBruteEntry item)
        {
            if (distributed == null)
            {
                cache.Set(cacheKey, item);
            }
            else
            {
                distributed.Set(cacheKey, Serialize(item));
            }
        }

        private void ClearExpiredItems(NoBruteEntry entry)
        {
            var expireIn = GetExpireTimespan();
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
            using var hasher = SHA512.Create();
            return string.Concat(hasher.ComputeHash(Encoding.UTF8.GetBytes(ip)).Select(b => b.ToString("x2")));
        }

        private TimeUntilResetUnit GetUnit(char value)
        {
            return Enum.TryParse<TimeUntilResetUnit>(value.ToString(), out var unit) ? unit : TimeUntilResetUnit.Hours;
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
        private static T Deserialize<T>(byte[] data) => System.Text.Json.JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data));
    }
}
