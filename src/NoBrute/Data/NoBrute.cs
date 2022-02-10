using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        /// <summary>
        /// The enabled
        /// </summary>
        private bool? enabled;

        /// <summary>
        /// The green retries
        /// </summary>
        private int? greenRetries;

        /// <summary>
        /// The increase request time ms
        /// </summary>
        private int? increaseRequestTimeMs;

        /// <summary>
        /// The time until reset
        /// </summary>
        private int? timeUntilReset;

        /// <summary>
        /// The time until reset unit
        /// </summary>
        private Models.TimeUntilResetUnit? timeUntilResetUnit;

        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger<NoBrute> logger;

        /// <summary>
        /// The HTTP context accessor
        /// </summary>
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>
        /// The cache
        /// </summary>
        private readonly IMemoryCache cache;

        /// <summary>
        /// The distributed
        /// </summary>
        private readonly IDistributedCache distributed;

        /// <summary>
        /// The status codes for automatic process
        /// </summary>
        private readonly int[] statusCodesForAutoProcess;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBrute"/> class.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <exception cref="NoBruteDependencyException">
        /// NoBrute requires MemoryCahce to be used. Install the Package: Microsoft.Extensions.Caching.Abstractions and use it in your ConfigureServices method with 'services.AddMemoryCache();'
        /// or
        /// IConfiguration was not found. Ensure you registered your configuration and its accessible.
        /// </exception>
        public NoBrute(IServiceProvider provider)
        {
            this.logger = provider.GetService<ILogger<NoBrute>>();
            this.httpContextAccessor = provider.GetService<IHttpContextAccessor>();
            // Get Services required for running
            IMemoryCache memoryCache = provider.GetService<IMemoryCache>();
            IDistributedCache distributedCache = provider.GetService<IDistributedCache>();

            IConfiguration config = provider.GetService<IConfiguration>();

            this.logger.LogDebug("Checking for Existence of MemoryCache Service...");

            if (memoryCache == null && distributedCache == null)
            {
                this.logger.LogError("No MemoryCache or IDistributedCache Service registered. Throwing exception.");
                throw new NoBruteDependencyException("NoBrute requires MemoryCache or IDistributedCache to be used. Install the Package: Microsoft.Extensions.Caching and use it in your ConfigureServices method with 'services.AddMemoryCache();'");
            }

            this.cache = memoryCache;

            this.distributed = distributedCache;

            this.logger.LogDebug("Memory Cache is registered. Getting Config.");

            if (config == null)
            {
                this.logger.LogError("Something went wrong with the IConfiguration Service.");
                throw new NoBruteDependencyException("IConfiguration was not found. Ensure you registered your configuration and its accessible.");
            }

            this.logger.LogDebug("Reading Config Entry \"NoBrute\"");
            IConfigurationSection section = config.GetSection("NoBrute");

            this.enabled = section?.GetValue<bool>("Enabled", true);
            this.greenRetries = section?.GetValue<int>("GreenRetries", 10);
            this.increaseRequestTimeMs = section?.GetValue<int>("IncreaseRequestTime", 20);
            this.timeUntilReset = section?.GetValue<int>("TimeUntilReset", 2);
            this.timeUntilResetUnit = this.GetUnit(section?.GetValue<char>("TimeUntilResetUnit", 'H'));
            IConfigurationSection statusCodes = section.GetSection("StatusCodesForAutoProcess");
            this.statusCodesForAutoProcess = statusCodes.AsEnumerable().Select(x => Convert.ToInt32(x.Value))?.ToArray() ?? new int[] { 200 };

            if (this.statusCodesForAutoProcess.Length == 0)
            {
                this.statusCodesForAutoProcess = new int[] { 200 };
            }

            this.logger.LogDebug("Config read. Validating configuration...");

            this.checkConfig(section);

            this.logger.LogDebug("Configuration is valid.");
        }

        /// <summary>
        /// Checks the request.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        public NoBruteRequestCheck CheckRequest(string requestName = null)
        {
            using (this.logger.BeginScope("CheckRequest"))
            {
                if (!this.enabled.Value)
                {
                    this.logger.LogDebug("NoBrute is disabled. Please Enable it via config. NoBrute->Enabled:true");
                    return null;
                }
                HttpRequest request = this.httpContextAccessor.HttpContext.Request;
                string name = requestName ?? request.Path;
                string ip = this.httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();
                string ip_censored = null;
                string cacheKey = this.generateCacheKey(ip);

                if (ip.Contains('.'))
                {
                    ip_censored = this.censoreIp(ip, '.');
                }
                else
                {
                    ip_censored = this.censoreIp(ip, ':');
                }

                this.logger.LogDebug($"Checking Request Entries for IP {ip_censored} by using cache key: {cacheKey}");
                Models.NoBruteEntry noBruteEntry = null;

                if (!this._TryGetCacheValue(cacheKey, out noBruteEntry))
                {
                    noBruteEntry = new NoBruteEntry();
                    noBruteEntry.IP = ip;
                    noBruteEntry.Requests = new List<NoBruteRequestItem>();
                }

                this.clearExpiredItems(noBruteEntry);
                NoBruteRequestItem requestItem = this.manageRequestEntry(noBruteEntry, name, request);

                this._SetCacheItem(cacheKey, noBruteEntry);
                this.logger.LogDebug("Added Cache Entry");
                this.logger.LogDebug("Checking request item...");

                NoBruteRequestCheck result = new NoBruteRequestCheck();

                result.IsGreenRequest = (requestItem.Hitcount <= this.greenRetries);
                result.RemoteAddr = ip;
                result.RequestNum = requestItem.Hitcount;
                result.ResetTime = requestItem.LastHit + this.GetExpireTimespan();

                this.logger.LogDebug($"Request {requestItem.RequestName} has HitCount of {requestItem.Hitcount}. Counter Reset at {result.ResetTime}");

                if (!result.IsGreenRequest)
                {
                    result.AppendRequestTime = (int)((requestItem.Hitcount - this.greenRetries) * this.increaseRequestTimeMs);

                    this.logger.LogDebug($"Request {requestItem.RequestName} is not a green request. Appending request deleay of {result.AppendRequestTime}ms");
                }

                return result;
            }
        }

        /// <summary>
        /// Releases the request and resets the request delay.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        public bool ReleaseRequest(string requestName = null)
        {
            HttpRequest request = this.httpContextAccessor.HttpContext.Request;
            string name = requestName ?? request.Path;
            string ip = this.httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();
            string cacheKey = this.generateCacheKey(ip);
            string ip_censored = null;

            if (ip.Contains('.'))
            {
                ip_censored = this.censoreIp(ip, '.');
            }
            else
            {
                ip_censored = this.censoreIp(ip, ':');
            }

            using (this.logger.BeginScope("ReleaseRequest"))
            {
                this.logger.LogDebug($"Trying to fetch cache entry >{cacheKey}< for IP >{ip_censored}<");
                NoBruteEntry entry = null;

                bool exists = this._TryGetCacheValue(cacheKey, out entry);

                if (!exists || entry == null)
                {
                    this.logger.LogDebug($"No Cache Entry found. Request allrdy released or never saved.");

                    return true;
                }

                this.logger.LogDebug($"Trying to find request with name >{name}<...");

                NoBruteRequestItem requestItem = this.manageRequestEntry(entry, name, request);

                if (requestItem != null)
                {
                    this.logger.LogDebug($"Found request in cache. Deleting...");

                    entry.Requests.Remove(requestItem);

                    this._SetCacheItem(cacheKey, entry);
                    this.logger.LogDebug($"Deleted request item >{cacheKey}.{name}<");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Release an given request if the status code is inside the configurable status codes for auto release.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        public bool AutoProcessRequestRelease(int status, string requestName = null)
        {
            if (this.statusCodesForAutoProcess.Any(x => x == status))
            {
                this.ReleaseRequest(requestName);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries the get cache value.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        private bool _TryGetCacheValue(string cacheKey, out NoBruteEntry item)
        {
            if (this.distributed == null)
                return this.cache.TryGetValue(cacheKey, out item);

            byte[] result = this.distributed.Get(cacheKey);

            if (result != null)
            {
                item = this._GetEntryFromByteArray(result);
                return true;
            }

            item = null;

            return false;
        }

        /// <summary>
        /// Sets the cache item.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="item">The item.</param>
        private void _SetCacheItem(string cacheKey, NoBruteEntry item)
        {
            if (this.distributed == null)
            {
                this.cache.Set<NoBruteEntry>(cacheKey, item);
            }
            else
            {
                string json = JsonConvert.SerializeObject(item);

                this.distributed.Set(cacheKey+".6", Encoding.UTF8.GetBytes(json)); // We changed the type of the cache entry serialization. NET6/NET5 versions will use JSON to format 
            }
        }

        

        /// <summary>
        /// Gets the entry from byte array.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        private NoBruteEntry _GetEntryFromByteArray(byte[] data)
        {
            if (data == null)
                return null;

            string json = Encoding.UTF8.GetString(data);

            return JsonConvert.DeserializeObject<NoBruteEntry>(json);
        }

        /// <summary>
        /// Manages the request entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        private NoBruteRequestItem manageRequestEntry(NoBruteEntry entry, string requestName, HttpRequest data)
        {
            if (entry.Requests.Any(x => x.RequestName == requestName && x.RequestMethod == data.Method))
            {
                NoBruteRequestItem item = entry.Requests.First(x => x.RequestName == requestName && x.RequestMethod == data.Method);
                item.Hitcount++;
                item.LastHit = DateTime.Now;

                return item;
            }
            else
            {
                NoBruteRequestItem item = new NoBruteRequestItem()
                {
                    Hitcount = 1,
                    RequestMethod = data.Method,
                    RequestName = requestName,
                    RequestPath = data.Path,
                    RequestQuery = data.QueryString.HasValue ? data.QueryString.ToString() : null,
                    LastHit = DateTime.Now
                };
                entry.Requests.Add(item);

                return item;
            }
        }

        /// <summary>
        /// Clears the expired items.
        /// </summary>
        /// <param name="entry">The entry.</param>
        private void clearExpiredItems(NoBruteEntry entry)
        {
            TimeSpan expireIn = this.GetExpireTimespan();

            switch (this.timeUntilResetUnit)
            {
                case TimeUntilResetUnit.Years:
                    expireIn = new TimeSpan(this.timeUntilReset.Value * 365, 0, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Months:
                    expireIn = new TimeSpan(this.timeUntilReset.Value * 30, 0, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Days:
                    expireIn = new TimeSpan(this.timeUntilReset.Value, 0, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Hours:
                    expireIn = new TimeSpan(0, this.timeUntilReset.Value, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Minutes:
                    expireIn = new TimeSpan(0, 0, this.timeUntilReset.Value, 0, 0);
                    break;

                case TimeUntilResetUnit.Seconds:
                    expireIn = new TimeSpan(0, 0, 0, this.timeUntilReset.Value, 0);
                    break;

                case TimeUntilResetUnit.Miliseconds:
                    expireIn = new TimeSpan(0, 0, 0, 0, this.timeUntilReset.Value);
                    break;
            }

            entry.Requests = entry.Requests.Where(x => !x.IsExpired(expireIn)).ToList();
        }

        /// <summary>
        /// Gets the expire timespan.
        /// </summary>
        /// <returns></returns>
        private TimeSpan GetExpireTimespan()
        {
            TimeSpan expireIn = new TimeSpan();

            switch (this.timeUntilResetUnit)
            {
                case TimeUntilResetUnit.Years:
                    expireIn = new TimeSpan(this.timeUntilReset.Value * 365, 0, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Months:
                    expireIn = new TimeSpan(this.timeUntilReset.Value * 30, 0, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Days:
                    expireIn = new TimeSpan(this.timeUntilReset.Value, 0, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Hours:
                    expireIn = new TimeSpan(0, this.timeUntilReset.Value, 0, 0, 0);
                    break;

                case TimeUntilResetUnit.Minutes:
                    expireIn = new TimeSpan(0, 0, this.timeUntilReset.Value, 0, 0);
                    break;

                case TimeUntilResetUnit.Seconds:
                    expireIn = new TimeSpan(0, 0, 0, this.timeUntilReset.Value, 0);
                    break;

                case TimeUntilResetUnit.Miliseconds:
                    expireIn = new TimeSpan(0, 0, 0, 0, this.timeUntilReset.Value);
                    break;
            }

            return expireIn;
        }

        /// <summary>
        /// Generates the cache key.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <returns></returns>
        private string generateCacheKey(string ip)
        {
            string key = null;

            using (SHA512 hasher = SHA512.Create())
            {
                byte[] data = Encoding.UTF8.GetBytes(ip);
                // Create a new Stringbuilder to collect the bytes
                // and create a string.
                var sBuilder = new StringBuilder();

                // Loop through each byte of the hashed data
                // and format each one as a hexadecimal string.
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }

                key = sBuilder.ToString(); ;
            }

            return key;
        }

        /// <summary>
        /// Gets the unit of expire.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        private Models.TimeUntilResetUnit? GetUnit(char? value)
        {
            if (value.HasValue)
            {
                return (TimeUntilResetUnit)value;
            }

            return null;
        }

        #nullable enable
        /// <summary>
        /// Checks the configuration.
        /// </summary>
        /// <param name="section">The section.</param>
        private void checkConfig(IConfigurationSection? section)
        {
            this.checkConfigEntry(section, "Enabled", typeof(bool), this.enabled, true);
            this.checkConfigEntry(section, "GreenRetries", typeof(int), this.greenRetries, 10);
            this.checkConfigEntry(section, "IncreaseRequestTime", typeof(int), this.increaseRequestTimeMs, 20);
            this.checkConfigEntry(section, "TimeUntilReset", typeof(int), this.timeUntilReset, 1);
            this.checkConfigEntry(section, "TimeUntilResetUnit", typeof(char), this.timeUntilResetUnit, 'd');
        }
        #nullable disable
        /// <summary>
        /// Checks the configuration entry.
        /// </summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="expectedType">The expected type.</param>
        /// <param name="currentvalue">The currentvalue.</param>
        /// <exception cref="NoBruteConfigurationException">Invalid value for configuration NoBrute->{name}. Value \"{section?.GetValue<string>(name, "NULL") ?? "NULL"}\" has to be of type {expectedType.FullName}</exception>
        private void checkConfigEntry(IConfigurationSection section, string name, Type expectedType, object currentvalue, object defaultValue)
        {
            this.logger.LogDebug($"Configuration Item NoBrute->{name} will be checked now...");
            if (currentvalue == null)
            {
                this.logger.LogError($"Configuration Value for Item NoBrute->{name} was Invalid.");
                throw new NoBruteConfigurationException($"Invalid value for configuration NoBrute->{name}. Value \"{section?.GetValue<string>(name, defaultValue.ToString()) ?? "NULL"}\" has to be of type {expectedType.FullName}");
            }

            this.logger.LogDebug($"Configuration Item NoBrute->{name} is valid with value {section?.GetValue<string>(name, defaultValue.ToString()) ?? "NULL"}");
        }

        /// <summary>
        /// Censores the ip.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="seperator">The seperator.</param>
        /// <returns></returns>
        private string censoreIp(string ip, char seperator)
        {
            return string.Join("", ip.ToCharArray().Select(x => '*').Select(x => x.ToString()));
        }
    }
}