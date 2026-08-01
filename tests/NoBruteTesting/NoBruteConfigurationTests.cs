using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoBrute.Data;
using NoBrute.Domain;
using NoBrute.Models;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace NoBruteTesting
{
    /// <summary>
    /// Tests for reading the new options from a real configuration source.
    /// </summary>
    public class NoBruteConfigurationTests : Abstracts.NoBruteTestCasesAbstract
    {
        #region Client IP Options

        /// <summary>
        /// It should fall back to the defaults when nothing is configured.
        /// </summary>
        [Fact]
        public void ItShouldUseClientIpDefaultsWhenNothingIsConfigured()
        {
            NoBruteClientIpOptions options = NoBruteClientIpOptions.FromConfiguration(BuildSection(new Dictionary<string, string>()));

            options.UseForwardedHeaders.ShouldBeFalse();
            options.ForwardLimit.ShouldBe(1);
            options.TrustLoopback.ShouldBeTrue();
            options.Headers.ShouldBe(new[] { "CF-Connecting-IP", "X-Forwarded-For" });
            options.KnownProxies.ShouldBeEmpty();
            options.KnownNetworks.ShouldBeEmpty();
        }

        /// <summary>
        /// It should not throw when the whole NoBrute section is missing.
        /// </summary>
        [Fact]
        public void ItShouldUseClientIpDefaultsWithoutAnySection()
        {
            NoBruteClientIpOptions options = NoBruteClientIpOptions.FromConfiguration(null);

            options.UseForwardedHeaders.ShouldBeFalse();
            options.Headers.ShouldNotBeEmpty();
        }

        /// <summary>
        /// It should read the client IP options from array style configuration.
        /// </summary>
        [Fact]
        public void ItShouldReadClientIpOptionsFromArrays()
        {
            NoBruteClientIpOptions options = NoBruteClientIpOptions.FromConfiguration(BuildSection(new Dictionary<string, string>
            {
                { "NoBrute:ClientIp:UseForwardedHeaders", "true" },
                { "NoBrute:ClientIp:ForwardLimit", "2" },
                { "NoBrute:ClientIp:TrustLoopback", "false" },
                { "NoBrute:ClientIp:Headers:0", "X-Real-IP" },
                { "NoBrute:ClientIp:KnownProxies:0", "172.68.0.1" },
                { "NoBrute:ClientIp:KnownProxies:1", "172.68.0.2" },
                { "NoBrute:ClientIp:KnownNetworks:0", "10.0.0.0/8" }
            }));

            options.UseForwardedHeaders.ShouldBeTrue();
            options.ForwardLimit.ShouldBe(2);
            options.TrustLoopback.ShouldBeFalse();
            options.Headers.ShouldBe(new[] { "X-Real-IP" });
            options.KnownProxies.ShouldBe(new[] { "172.68.0.1", "172.68.0.2" });
            options.KnownNetworks.ShouldBe(new[] { "10.0.0.0/8" });
        }

        /// <summary>
        /// It should also accept comma separated lists, which is how environment variables are usually passed.
        /// </summary>
        [Fact]
        public void ItShouldReadClientIpListsFromCommaSeparatedValues()
        {
            NoBruteClientIpOptions options = NoBruteClientIpOptions.FromConfiguration(BuildSection(new Dictionary<string, string>
            {
                { "NoBrute:ClientIp:Headers", "CF-Connecting-IP, X-Real-IP" },
                { "NoBrute:ClientIp:KnownNetworks", "10.0.0.0/8,172.16.0.0/12" }
            }));

            options.Headers.ShouldBe(new[] { "CF-Connecting-IP", "X-Real-IP" });
            options.KnownNetworks.ShouldBe(new[] { "10.0.0.0/8", "172.16.0.0/12" });
        }

        /// <summary>
        /// It should keep the defaults instead of throwing when a value cannot be parsed.
        /// </summary>
        [Fact]
        public void ItShouldIgnoreMalformedClientIpValues()
        {
            NoBruteClientIpOptions options = NoBruteClientIpOptions.FromConfiguration(BuildSection(new Dictionary<string, string>
            {
                { "NoBrute:ClientIp:UseForwardedHeaders", "yes-please" },
                { "NoBrute:ClientIp:ForwardLimit", "many" }
            }));

            options.UseForwardedHeaders.ShouldBeFalse();
            options.ForwardLimit.ShouldBe(1);
        }

        #endregion Client IP Options

        #region Entry Limiter Options

        /// <summary>
        /// It should read the entry limit from configuration.
        /// </summary>
        [Fact]
        public void ItShouldReadTheEntryLimitFromConfiguration()
        {
            IServiceProvider provider = BuildProvider(new Dictionary<string, string>
            {
                { "NoBrute:MaxTrackedEntries", "50000" }
            });

            new NoBruteEntryLimiter(provider).MaxTrackedEntries.ShouldBe(50000);
        }

        /// <summary>
        /// It should default to unlimited and ignore malformed or negative values.
        /// </summary>
        /// <param name="configured">The configured value.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-number")]
        [InlineData("-5")]
        public void ItShouldFallBackToUnlimitedTracking(string configured)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            if (configured != null)
            {
                values.Add("NoBrute:MaxTrackedEntries", configured);
            }

            new NoBruteEntryLimiter(BuildProvider(values)).MaxTrackedEntries.ShouldBe(0);
        }

        /// <summary>
        /// It should prefer the value set in code over the configured one.
        /// </summary>
        [Fact]
        public void ItShouldPreferTheEntryLimitSetInCode()
        {
            IServiceProvider provider = BuildProvider(new Dictionary<string, string>
            {
                { "NoBrute:MaxTrackedEntries", "50000" }
            });

            new NoBruteEntryLimiter(provider, 10).MaxTrackedEntries.ShouldBe(10);
        }

        #endregion Entry Limiter Options

        #region Service Options

        /// <summary>
        /// It should cap the appended request time at MaxIncreaseRequestTime.
        /// </summary>
        /// <param name="maxIncreaseRequestTime">The configured cap.</param>
        /// <param name="expectedAppendRequestTime">The expected delay.</param>
        [Theory]
        [InlineData("0", 1000)]
        [InlineData("250", 250)]
        [InlineData("5000", 1000)]
        public void ItShouldCapTheAppendedRequestTime(string maxIncreaseRequestTime, int expectedAppendRequestTime)
        {
            this.RegisterDeadMocks();
            this.RegisterConfiguration(new Dictionary<string, string>
            {
                { "NoBrute:GreenRetries", "5" },
                { "NoBrute:IncreaseRequestTime", "10" },
                { "NoBrute:MaxIncreaseRequestTime", maxIncreaseRequestTime }
            });
            this.RegisterRealMemoryCache();
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());
            NoBruteRequestCheck check = null;

            // 105 hits => (105 - 5) * 10ms = 1000ms before capping
            for (int i = 0; i < 105; i++)
            {
                check = noBrute.CheckRequest("FALSY_REQUEST");
            }

            check.AppendRequestTime.ShouldBe(expectedAppendRequestTime);
        }

        /// <summary>
        /// It should read the blocked status code from configuration.
        /// </summary>
        [Fact]
        public void ItShouldReadTheBlockedStatusCodeFromConfiguration()
        {
            this.RegisterDeadMocks();
            this.RegisterConfiguration(new Dictionary<string, string>
            {
                { "NoBrute:BlockedStatusCode", "503" }
            });
            this.RegisterRealMemoryCache();
            this.MockRequest();

            INoBruteEntryLimiter limiter = this.RegisterEntryLimiter(1);
            limiter.TryTrack("someone-else", TimeSpan.FromMinutes(5)).ShouldBeTrue();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck check = noBrute.CheckRequest("FLOODED_REQUEST");

            check.IsBlocked.ShouldBeTrue();
            check.BlockedStatusCode.ShouldBe(503);
        }

        /// <summary>
        /// It should honor every documented time unit when calculating the reset time.
        /// </summary>
        /// <param name="unit">The configured unit character.</param>
        /// <param name="expectedSeconds">The expected lifetime in seconds for a value of 2.</param>
        [Theory]
        [InlineData("n", 2d / 1000)]
        [InlineData("s", 2)]
        [InlineData("i", 2 * 60)]
        [InlineData("H", 2 * 60 * 60)]
        [InlineData("d", 2 * 24 * 60 * 60)]
        [InlineData("M", 2 * 30 * 24 * 60 * 60)]
        [InlineData("y", 2d * 365 * 24 * 60 * 60)]
        [InlineData("?", 2 * 60 * 60)] // unknown units fall back to hours
        public void ItShouldHonorTheConfiguredTimeUnit(string unit, double expectedSeconds)
        {
            this.RegisterDeadMocks();
            this.RegisterConfiguration(new Dictionary<string, string>
            {
                { "NoBrute:TimeUntilReset", "2" },
                { "NoBrute:TimeUntilResetUnit", unit }
            });
            this.RegisterRealMemoryCache();
            this.MockRequest();

            DateTime before = DateTime.Now;
            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck check = noBrute.CheckRequest("ANY_REQUEST");

            check.ResetTime.ShouldBeInRange(
                before.AddSeconds(expectedSeconds).AddSeconds(-5),
                DateTime.Now.AddSeconds(expectedSeconds).AddSeconds(5));
        }

        /// <summary>
        /// It should keep working when the NoBrute section does not exist at all.
        /// </summary>
        [Fact]
        public void ItShouldUseDefaultsWithoutAnyConfiguration()
        {
            this.RegisterDeadMocks();
            this.RegisterConfiguration(new Dictionary<string, string>());
            this.RegisterRealMemoryCache();
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck check = noBrute.CheckRequest("ANY_REQUEST");

            check.ShouldNotBeNull();
            check.IsGreenRequest.ShouldBeTrue();
            check.IsBlocked.ShouldBeFalse();
            check.BlockedStatusCode.ShouldBe(429);
            check.AppendRequestTime.ShouldBe(0);
        }

        /// <summary>
        /// It should do nothing at all when disabled.
        /// </summary>
        [Fact]
        public void ItShouldReturnNothingWhenDisabled()
        {
            this.RegisterDeadMocks();
            this.RegisterConfiguration(new Dictionary<string, string>
            {
                { "NoBrute:Enabled", "false" }
            });
            this.RegisterRealMemoryCache();
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            noBrute.CheckRequest("ANY_REQUEST").ShouldBeNull();
        }

        #endregion Service Options

        private static IConfigurationSection BuildSection(IDictionary<string, string> values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build()
                .GetSection("NoBrute");
        }

        private static IServiceProvider BuildProvider(IDictionary<string, string> values)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

            return services.BuildServiceProvider();
        }
    }
}
