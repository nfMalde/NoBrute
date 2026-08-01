using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NoBrute.Data;
using NoBrute.Domain;
using NoBrute.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NoBruteTesting.Abstracts
{
    /// <summary>
    /// Helper Methods / Abstracts / Fields for NoBruteService Test Cases
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.Base.TestCaseAbstractBase" />
    public abstract class NoBruteTestCasesAbstract : Base.TestCaseAbstractBase
    {
        #region Fields

        /// <summary>
        /// Cache method delegate to used in mock objects as callback
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="outPut">The out put.</param>
        protected delegate void cacheDel(object cacheKey, out object outPut);

        #endregion Fields

        #region Helper Methods

        /// <summary>
        /// Mocks the request.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="remoteIp">The socket peer address.</param>
        /// <param name="headers">Request headers.</param>
        /// <returns>The mocked context. Mutate it to simulate follow up requests of other clients.</returns>
        protected DefaultHttpContext MockRequest(int statusCode = 200, string remoteIp = "127.0.0.1", IDictionary<string, string> headers = null)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/";
            context.Response.StatusCode = statusCode;

            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);

            foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
            {
                context.Request.Headers[header.Key] = header.Value;
            }

            Mock<IHttpContextAccessor> contextMock = new Mock<IHttpContextAccessor>();
            contextMock.Setup(x => x.HttpContext).Returns(context);

            this.provider.AddScoped<IHttpContextAccessor>(x => contextMock.Object);

            return context;
        }

        /// <summary>
        /// Registers an <see cref="IHttpContextAccessor"/> without a current request.
        /// </summary>
        protected void MockMissingRequest()
        {
            Mock<IHttpContextAccessor> contextMock = new Mock<IHttpContextAccessor>();
            contextMock.Setup(x => x.HttpContext).Returns(value: null);

            this.provider.AddScoped<IHttpContextAccessor>(x => contextMock.Object);
        }

        /// <summary>
        /// Registers a real in memory configuration instead of a mocked one, so the actual
        /// configuration binding is exercised.
        /// </summary>
        /// <param name="values">The configuration values (e.g. <c>NoBrute:GreenRetries</c>).</param>
        protected void RegisterConfiguration(IDictionary<string, string> values)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values ?? new Dictionary<string, string>())
                .Build();

            this.provider.AddSingleton<IConfiguration>(configuration);
        }

        /// <summary>
        /// Registers a real memory cache, so entries really are stored, read back and expired.
        /// </summary>
        protected void RegisterRealMemoryCache()
        {
            this.provider.AddMemoryCache();
        }

        /// <summary>
        /// Registers the mock memory cache.
        /// </summary>
        /// <param name="desiredReturnEntry">The desired return entry.</param>
        /// <returns>The mocked cache entry, to verify how it was written.</returns>
        protected Mock<ICacheEntry> RegisterMockMemoryCache(NoBruteEntry desiredReturnEntry)
        {
            Mock<ICacheEntry> mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.Setup(x => x.Value).Returns(value: null);
            mockCacheEntry.Setup(x => x.Dispose());

            NoBruteEntry outVal = desiredReturnEntry;
            Mock<IMemoryCache> cacheMock = new Mock<IMemoryCache>();
            cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(true)
                .Callback(new cacheDel((object k, out object entry) =>
                {
                    entry = desiredReturnEntry;
                }));
            cacheMock.Setup(x => x.CreateEntry(It.IsAny<string>())).Returns(mockCacheEntry.Object);

            this.provider.AddScoped<IMemoryCache>(x => cacheMock.Object);
            this.memoryCacheMock = cacheMock;

            return mockCacheEntry;
        }

        /// <summary>
        /// Gets the memory cache mock registered by <see cref="RegisterMockMemoryCache"/>.
        /// </summary>
        protected Mock<IMemoryCache> memoryCacheMock;

        /// <summary>
        /// Registers a memory cache mock that never returns an entry (cache miss for every client).
        /// </summary>
        protected Mock<IMemoryCache> RegisterEmptyMockMemoryCache()
        {
            Mock<ICacheEntry> mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.Setup(x => x.Dispose());

            Mock<IMemoryCache> cacheMock = new Mock<IMemoryCache>();
            cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(false)
                .Callback(new cacheDel((object k, out object entry) =>
                {
                    entry = null;
                }));
            cacheMock.Setup(x => x.CreateEntry(It.IsAny<string>())).Returns(mockCacheEntry.Object);

            this.provider.AddScoped<IMemoryCache>(x => cacheMock.Object);

            return cacheMock;
        }

        /// <summary>
        /// Registers the default client IP resolver with the given options.
        /// </summary>
        /// <param name="options">The client IP options.</param>
        protected void RegisterClientIpResolver(NoBruteClientIpOptions options)
        {
            this.provider.AddSingleton<INoBruteClientIpResolver>(f => new NoBruteClientIpResolver(f, options));
        }

        /// <summary>
        /// Registers the entry limiter with the given maximum.
        /// </summary>
        /// <param name="maxTrackedEntries">The maximum number of tracked entries.</param>
        protected INoBruteEntryLimiter RegisterEntryLimiter(int maxTrackedEntries)
        {
            NoBruteEntryLimiter limiter = new NoBruteEntryLimiter(maxTrackedEntries);
            this.provider.AddSingleton<INoBruteEntryLimiter>(f => limiter);

            return limiter;
        }

        /// <summary>
        /// Registers the mock distributed cache.
        /// </summary>
        /// <param name="desiredReturnEntry">The desired return entry.</param>
        /// <returns>The mocked distributed cache, to verify how it was used.</returns>
        protected Mock<IDistributedCache> RegisterMockDistributedCache(NoBruteEntry desiredReturnEntry)
        {
            Mock<IDistributedCache> mock = new Mock<IDistributedCache>();

            string json = JsonConvert.SerializeObject(desiredReturnEntry);


            mock.Setup(x => x.Get(It.IsAny<string>())).Returns(Encoding.UTF8.GetBytes(json));
            mock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.UTF8.GetBytes(json));
            mock.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>()));
            mock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            this.provider.AddScoped<IDistributedCache>((f) => mock.Object);

            return mock;
        }

        /// <summary>
        /// Registers the dead mocks. (Such as logger)
        /// </summary>
        protected void RegisterDeadMocks()
        {
            //Add Logger
            this.provider.AddLogging(configure =>
            {
                configure.SetMinimumLevel(LogLevel.Debug);
            });
        }

        /// <summary>
        /// Mocks the configuration.
        /// </summary>
        /// <param name="enabled">The enabled.</param>
        /// <param name="greenRetries">The green retries.</param>
        /// <param name="increaseTime">The increase time.</param>
        /// <param name="timeUntilReset">The time until reset.</param>
        /// <param name="timeUntilResetUnit">The time until reset unit.</param>
        /// <param name="statusCodes">The status codes.</param>
        protected void MockConfig(bool? enabled, int? greenRetries, int? increaseTime, int? timeUntilReset, char? timeUntilResetUnit = 'H', int[] statusCodes = null)
        {
            Mock<IConfigurationSection> mockSection = new Mock<IConfigurationSection>();
            mockSection.Setup(x => x.GetSection("Enabled")).Returns(this.GetValueSection(enabled));
            mockSection.Setup(x => x.GetSection("GreenRetries")).Returns(this.GetValueSection(greenRetries));
            mockSection.Setup(x => x.GetSection("IncreaseRequestTime")).Returns(this.GetValueSection(increaseTime));
            mockSection.Setup(x => x.GetSection("TimeUntilReset")).Returns(this.GetValueSection(timeUntilReset));
            mockSection.Setup(x => x.GetSection("TimeUntilResetUnit")).Returns(this.GetValueSection(timeUntilResetUnit));

            // Status Code Section
            statusCodes = statusCodes ?? new int[] { 200 };

            List<IConfigurationSection> sections = new List<IConfigurationSection>();
            for (int i = 0; i < statusCodes.Length; i++)
            {
                Mock<IConfigurationSection> mockCode = new Mock<IConfigurationSection>();
                mockCode.Setup(x => x.Path).Returns($"StatusCodesForAutoProcess:{i}");
                mockCode.Setup(x => x.Value).Returns(statusCodes[i].ToString());

                sections.Add(mockCode.Object);
            }

            IEnumerable<KeyValuePair<string, string>> pairs = statusCodes.Select(code => code.ToString()).Select(s => new KeyValuePair<string, string>(s, s));

            Mock<IConfigurationSection> mockStatusCodeSection = new Mock<IConfigurationSection>();
            mockStatusCodeSection
                .Setup(x => x.Value).Returns(value: null);
            mockStatusCodeSection.Setup(x => x.Path).Returns("StatusCodesForAutoProcess");
            mockStatusCodeSection.Setup(x => x.GetChildren()).Returns(sections);

            mockSection.Setup(x => x.GetSection("StatusCodesForAutoProcess")).Returns(mockStatusCodeSection.Object);

            Mock<IConfiguration> configMock = new Mock<IConfiguration>();
            configMock.Setup(x => x.GetSection("NoBrute")).Returns(mockSection.Object);

            this.provider.AddScoped<IConfiguration>((f) => configMock.Object);
        }

        /// <summary>
        /// Gets the value section  mock for an ConfigurationSection.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        protected IConfigurationSection GetValueSection(object value)
        {
            Mock<IConfigurationSection> section = new Mock<IConfigurationSection>();
            section.Setup(x => x.Value).Returns(value?.ToString());

            return section.Object;
        }

        #endregion Helper Methods
    }
}