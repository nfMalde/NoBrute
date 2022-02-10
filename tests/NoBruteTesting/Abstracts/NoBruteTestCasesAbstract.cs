using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NoBrute.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

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
        protected void MockRequest(int statusCode = 200)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/";
            context.Response.StatusCode = statusCode;

            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

            Mock<IHttpContextAccessor> contextMock = new Mock<IHttpContextAccessor>();
            contextMock.Setup(x => x.HttpContext).Returns(context);

            this.provider.AddScoped<IHttpContextAccessor>(x => contextMock.Object);
        }

        /// <summary>
        /// Registers the mock memory cache.
        /// </summary>
        /// <param name="desiredReturnEntry">The desired return entry.</param>
        protected void RegisterMockMemoryCache(NoBruteEntry desiredReturnEntry)
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
        }

        /// <summary>
        /// Registers the mock distributed cache.
        /// </summary>
        /// <param name="desiredReturnEntry">The desired return entry.</param>
        protected void RegisterMockDistributedCache(NoBruteEntry desiredReturnEntry)
        {
            Mock<IDistributedCache> mock = new Mock<IDistributedCache>();

            string json = JsonConvert.SerializeObject(desiredReturnEntry);


            mock.Setup(x => x.Get(It.IsAny<string>())).Returns(Encoding.UTF8.GetBytes(json));
            mock.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>()));

            this.provider.AddScoped<IDistributedCache>((f) => mock.Object);
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