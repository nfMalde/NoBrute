using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoBrute.Domain;
using NoBrute.Models;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoBruteTesting
{
    /// <summary>
    /// Tests for the NoBruteService.
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.NoBruteTestCasesAbstract" />
    public class NoBruteServiceTests : Abstracts.NoBruteTestCasesAbstract
    {
        #region Global Test Cases

        /// <summary>
        /// Tests if exception is thrown when no cache module is registered to service provider
        /// </summary>
        [Fact]
        public void ItShouldFailIfNoCacheServiceIsRegistered()
        {
            Assert.Throws<NoBrute.Exceptions.NoBruteDependencyException>(() =>
            {
                this.RegisterDeadMocks();
                this.MockConfig(true, 5, 10, 2);
                this.MockRequest();

                INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());
            });
        }

        #endregion Global Test Cases

        #region Mixed Cache Test Cases

        /// <summary>
        /// It should handle request release for correct status code.
        /// </summary>
        /// <param name="statusCodeForAutoRelease">The status code for automatic release.</param>
        /// <param name="expectedStatusCode">The expected status code.</param>
        [Theory]
        [InlineData(200, 200)]
        [InlineData(200, 401)]
        [InlineData(200, 500)]
        [InlineData(200, 404)]
        public void ItShouldHandleRequestReleaseForCorrectStatusCode(
            int statusCodeForAutoRelease,
            int expectedStatusCode
            )
        {
            List<string> cacheTypes = new List<string>()
                {
                    "Memory",
                    "Distributed"
                };

            foreach (string cacheType in cacheTypes)
            {
                string requestName = "FALSY_REQUEST";
                NoBruteEntry entry = new NoBruteEntry();
                entry.IP = "127.0.0.1";
                entry.Requests = new List<NoBruteRequestItem>();

                // Green Retries is 5.  So fake the request hit count
                entry.Requests.Add(new NoBruteRequestItem()
                {
                    Hitcount = 5,
                    LastHit = DateTime.Now,
                    RequestMethod = "GET",
                    RequestName = requestName,
                    RequestPath = "/",
                    RequestQuery = ""
                });

                int requestsBefore = entry.Requests.Count;

                this.RegisterDeadMocks();
                this.MockConfig(true, 5, 10, 2, 'H', new int[] { statusCodeForAutoRelease });
                this.MockRequest(expectedStatusCode);
                this.RegisterMockMemoryCache(entry);

                INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

                noBrute.AutoProcessRequestRelease(expectedStatusCode, requestName);

                if (statusCodeForAutoRelease == expectedStatusCode)
                {
                    // In this case we expect that the requests got released
                    entry.Requests.ShouldBeEmpty($"Request not removed at cache type:{cacheType}");
                }
                else
                {
                    // Here we expect that the Requests got not released
                    entry.Requests.Count.ShouldBe(requestsBefore, $"Request unexpected removed at cache type:{cacheType}");
                }
            }
        }

        #endregion Mixed Cache Test Cases

        #region Memory Cache Test Cases

        /// <summary>
        /// It should not increase request time if green request with memory cache.
        /// </summary>
        [Fact]
        public void ItShouldNotIncreaseRequestTimeIfGreenRequestWithMemoryCache()
        {
            // Entry
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            // Init Mocks
            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterMockMemoryCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest("GREEN_REQUEST");

            rCheck.AppendRequestTime.ShouldBe(0);
            rCheck.IsGreenRequest.ShouldBeTrue();
        }

        /// <summary>
        /// It should increase request time if exceeded green requests count memory cache.
        /// </summary>
        [Fact]
        public void ItShouldIncreaseRequestTimeIfExceededGreenRequestsCountMemoryCache()
        {
            string requestName = "FALSY_REQUEST";

            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            // Green Retries is 5.  So fake the request hit count
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = requestName,
                RequestPath = "/",
                RequestQuery = ""
            });

            // Init Mocks
            this.RegisterDeadMocks();
            this.MockConfig(true, greenRetries: 5, increaseTime: 10, 2);
            this.RegisterMockMemoryCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest(requestName);

            rCheck.ShouldNotBeNull();
            rCheck.IsGreenRequest.ShouldBeFalse();
            rCheck.AppendRequestTime.ShouldBe(10);
        }

        #endregion Memory Cache Test Cases

        #region Distributed Cache Test Cases

        /// <summary>
        /// It should not increase request time if green request with distributed cache.
        /// </summary>
        [Fact]
        public void ItShouldNotIncreaseRequestTimeIfGreenRequestWithDistributedCache()
        {
            // Entry
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            // Init Mocks
            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterMockDistributedCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest("GREEN_REQUEST");

            rCheck.AppendRequestTime.ShouldBe(0);
            rCheck.IsGreenRequest.ShouldBeTrue();
        }

        /// <summary>
        /// It should increase request time if exceeded green requests count distributed cache.
        /// </summary>
        [Fact]
        public void ItShouldIncreaseRequestTimeIfExceededGreenRequestsCountDistributedCache()
        {
            string requestName = "FALSY_REQUEST";

            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            // Green Retries is 5.  So fake the request hit count
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = requestName,
                RequestPath = "/",
                RequestQuery = ""
            });

            // Init Mocks
            this.RegisterDeadMocks();
            this.MockConfig(true, greenRetries: 5, increaseTime: 10, 2);
            this.RegisterMockDistributedCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest(requestName);

            rCheck.ShouldNotBeNull();
            rCheck.IsGreenRequest.ShouldBeFalse();
            rCheck.AppendRequestTime.ShouldBe(10);
        }

        #endregion Distributed Cache Test Cases

        #region Async Test Cases

        /// <summary>
        /// It should increase request time if exceeded green requests count on the asynchronous path.
        /// </summary>
        [Fact]
        public async Task ItShouldIncreaseRequestTimeIfExceededGreenRequestsCountAsync()
        {
            string requestName = "FALSY_REQUEST";

            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = requestName,
                RequestPath = "/",
                RequestQuery = ""
            });

            this.RegisterDeadMocks();
            this.MockConfig(true, greenRetries: 5, increaseTime: 10, 2);
            this.RegisterMockDistributedCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = await noBrute.CheckRequestAsync(requestName);

            rCheck.ShouldNotBeNull();
            rCheck.IsGreenRequest.ShouldBeFalse();
            rCheck.AppendRequestTime.ShouldBe(10);
        }

        /// <summary>
        /// It should release requests on the asynchronous path.
        /// </summary>
        [Fact]
        public async Task ItShouldHandleRequestReleaseAsync()
        {
            string requestName = "FALSY_REQUEST";

            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = requestName,
                RequestPath = "/",
                RequestQuery = ""
            });

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2, 'H', new int[] { 200 });
            this.MockRequest(200);
            this.RegisterMockMemoryCache(entry);

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            (await noBrute.AutoProcessRequestReleaseAsync(200, requestName)).ShouldBeTrue();
            entry.Requests.ShouldBeEmpty();
        }

        #endregion Async Test Cases

        #region Client IP Test Cases

        /// <summary>
        /// It should use the forwarded client IP when the request comes through a trusted proxy.
        /// </summary>
        [Fact]
        public void ItShouldUseTheForwardedClientIp()
        {
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "172.68.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterMockMemoryCache(entry);
            this.MockRequest(200, "172.68.0.1", new Dictionary<string, string>
            {
                { "CF-Connecting-IP", "198.51.100.23" }
            });
            this.RegisterClientIpResolver(new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                KnownProxies = new List<string> { "172.68.0.1" }
            });

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest("PROXIED_REQUEST");

            rCheck.RemoteAddr.ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should keep using the proxy address when forwarded headers are not enabled.
        /// </summary>
        [Fact]
        public void ItShouldIgnoreForwardedHeadersByDefault()
        {
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "172.68.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterMockMemoryCache(entry);
            this.MockRequest(200, "172.68.0.1", new Dictionary<string, string>
            {
                { "CF-Connecting-IP", "198.51.100.23" }
            });

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest("PROXIED_REQUEST");

            rCheck.RemoteAddr.ShouldBe("172.68.0.1");
        }

        #endregion Client IP Test Cases

        #region Entry Limit Test Cases

        /// <summary>
        /// It should hard block unknown clients once the tracked entry limit is reached.
        /// </summary>
        [Fact]
        public void ItShouldHardBlockUnknownClientsWhenTheEntryLimitIsReached()
        {
            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            Mock<IMemoryCache> cacheMock = this.RegisterEmptyMockMemoryCache();
            this.MockRequest();

            INoBruteEntryLimiter limiter = this.RegisterEntryLimiter(1);
            limiter.TryTrack("already-tracked-client", TimeSpan.FromMinutes(5)).ShouldBeTrue();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest("FLOODED_REQUEST");

            rCheck.ShouldNotBeNull();
            rCheck.IsBlocked.ShouldBeTrue();
            rCheck.IsGreenRequest.ShouldBeFalse();
            rCheck.BlockedStatusCode.ShouldBe(429);

            // Nothing is written to the cache anymore, which is the whole point of the circuit breaker.
            cacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Never());
        }

        /// <summary>
        /// It should behave normally while the entry limit is not reached.
        /// </summary>
        [Fact]
        public void ItShouldTrackClientsWhileBelowTheEntryLimit()
        {
            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterEmptyMockMemoryCache();
            this.MockRequest();

            INoBruteEntryLimiter limiter = this.RegisterEntryLimiter(10);

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck rCheck = noBrute.CheckRequest("GREEN_REQUEST");

            rCheck.IsBlocked.ShouldBeFalse();
            rCheck.IsGreenRequest.ShouldBeTrue();
            limiter.TrackedEntries.ShouldBe(1);
        }

        #endregion Entry Limit Test Cases

        #region Cache Lifetime Test Cases

        /// <summary>
        /// It should write memory cache entries with an absolute expiration, so they cannot pile up forever.
        /// </summary>
        [Fact]
        public void ItShouldExpireMemoryCacheEntries()
        {
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2, 'H');
            Mock<ICacheEntry> cacheEntryMock = this.RegisterMockMemoryCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            noBrute.CheckRequest("ANY_REQUEST");

            cacheEntryMock.VerifySet(x => x.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2), Times.Once());
        }

        /// <summary>
        /// It should write distributed cache entries with an absolute expiration.
        /// </summary>
        [Fact]
        public async Task ItShouldExpireDistributedCacheEntries()
        {
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2, 'H');
            Mock<IDistributedCache> cacheMock = this.RegisterMockDistributedCache(entry);
            this.MockRequest();

            DistributedCacheEntryOptions usedOptions = null;
            cacheMock
                .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, value, options, token) => usedOptions = options)
                .Returns(Task.CompletedTask);

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            await noBrute.CheckRequestAsync("ANY_REQUEST");

            usedOptions.ShouldNotBeNull();
            usedOptions.AbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromHours(2));
        }

        /// <summary>
        /// It should drop the whole cache entry and free the circuit breaker slot
        /// once the last request of a client is released.
        /// </summary>
        [Fact]
        public void ItShouldRemoveEmptyEntriesOnRelease()
        {
            string requestName = "FALSY_REQUEST";

            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = requestName,
                RequestPath = "/",
                RequestQuery = ""
            });

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterMockMemoryCache(entry);
            this.MockRequest();

            INoBruteEntryLimiter limiter = this.RegisterEntryLimiter(10);

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            noBrute.CheckRequest(requestName);
            limiter.TrackedEntries.ShouldBe(1);

            noBrute.ReleaseRequest(requestName).ShouldBeTrue();

            entry.Requests.ShouldBeEmpty();
            this.memoryCacheMock.Verify(x => x.Remove(It.IsAny<object>()), Times.Once());
            limiter.TrackedEntries.ShouldBe(0);
        }

        /// <summary>
        /// It should keep the entry when other requests of the same client are still tracked.
        /// </summary>
        [Fact]
        public void ItShouldKeepEntriesWithRemainingRequestsOnRelease()
        {
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = "FIRST_REQUEST",
                RequestPath = "/",
                RequestQuery = ""
            });
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = "SECOND_REQUEST",
                RequestPath = "/",
                RequestQuery = ""
            });

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterMockMemoryCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            noBrute.ReleaseRequest("FIRST_REQUEST").ShouldBeTrue();

            entry.Requests.Count.ShouldBe(1);
            this.memoryCacheMock.Verify(x => x.Remove(It.IsAny<object>()), Times.Never());
        }

        /// <summary>
        /// It should report that there was nothing to release for an unknown request name.
        /// </summary>
        [Fact]
        public void ItShouldReportUnknownRequestsOnRelease()
        {
            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterMockMemoryCache(entry);
            this.MockRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            noBrute.ReleaseRequest("NEVER_SEEN_REQUEST").ShouldBeFalse();
        }

        #endregion Cache Lifetime Test Cases

        #region Robustness Test Cases

        /// <summary>
        /// It should do nothing instead of throwing when there is no current HTTP request.
        /// </summary>
        [Fact]
        public async Task ItShouldDoNothingWithoutAnHttpContext()
        {
            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterEmptyMockMemoryCache();
            this.MockMissingRequest();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            noBrute.CheckRequest("ANY_REQUEST").ShouldBeNull();
            (await noBrute.CheckRequestAsync("ANY_REQUEST")).ShouldBeNull();
            noBrute.ReleaseRequest("ANY_REQUEST").ShouldBeTrue();
            (await noBrute.ReleaseRequestAsync("ANY_REQUEST")).ShouldBeTrue();
        }

        /// <summary>
        /// It should hard block on the asynchronous path as well.
        /// </summary>
        [Fact]
        public async Task ItShouldHardBlockOnTheAsynchronousPath()
        {
            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2);
            this.RegisterEmptyMockMemoryCache();
            this.MockRequest();

            INoBruteEntryLimiter limiter = this.RegisterEntryLimiter(1);
            limiter.TryTrack("already-tracked-client", TimeSpan.FromMinutes(5)).ShouldBeTrue();

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck check = await noBrute.CheckRequestAsync("FLOODED_REQUEST");

            check.IsBlocked.ShouldBeTrue();
            check.IsGreenRequest.ShouldBeFalse();
            check.AppendRequestTime.ShouldBe(0);
        }

        /// <summary>
        /// It should not release requests for status codes that are not configured for auto processing.
        /// </summary>
        [Fact]
        public async Task ItShouldNotAutoReleaseForOtherStatusCodesAsync()
        {
            string requestName = "FALSY_REQUEST";

            NoBruteEntry entry = new NoBruteEntry();
            entry.IP = "127.0.0.1";
            entry.Requests = new List<NoBruteRequestItem>();
            entry.Requests.Add(new NoBruteRequestItem()
            {
                Hitcount = 5,
                LastHit = DateTime.Now,
                RequestMethod = "GET",
                RequestName = requestName,
                RequestPath = "/",
                RequestQuery = ""
            });

            this.RegisterDeadMocks();
            this.MockConfig(true, 5, 10, 2, 'H', new int[] { 200 });
            this.RegisterMockMemoryCache(entry);
            this.MockRequest(401);

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            (await noBrute.AutoProcessRequestReleaseAsync(401, requestName)).ShouldBeFalse();
            entry.Requests.Count.ShouldBe(1);
        }

        /// <summary>
        /// It should count clients separately, so one attacker cannot slow down everybody else.
        /// </summary>
        [Fact]
        public void ItShouldTrackClientsIndependently()
        {
            this.RegisterDeadMocks();
            this.RegisterConfiguration(new Dictionary<string, string>
            {
                { "NoBrute:GreenRetries", "2" },
                { "NoBrute:IncreaseRequestTime", "10" }
            });
            this.RegisterRealMemoryCache();
            DefaultHttpContext context = this.MockRequest(200, "203.0.113.7");

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck attacker = null;
            for (int i = 0; i < 5; i++)
            {
                attacker = noBrute.CheckRequest("LOGIN");
            }

            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.99");
            NoBruteRequestCheck honestCustomer = noBrute.CheckRequest("LOGIN");

            attacker.IsGreenRequest.ShouldBeFalse();
            attacker.AppendRequestTime.ShouldBe(30);
            honestCustomer.IsGreenRequest.ShouldBeTrue();
            honestCustomer.AppendRequestTime.ShouldBe(0);
        }

        /// <summary>
        /// It should give every forwarded client its own entry instead of lumping them together
        /// behind the proxy address.
        /// </summary>
        [Fact]
        public void ItShouldTrackForwardedClientsIndependently()
        {
            this.RegisterDeadMocks();
            this.RegisterConfiguration(new Dictionary<string, string>
            {
                { "NoBrute:GreenRetries", "2" },
                { "NoBrute:IncreaseRequestTime", "10" },
                { "NoBrute:ClientIp:UseForwardedHeaders", "true" },
                { "NoBrute:ClientIp:KnownProxies:0", "172.68.0.1" }
            });
            this.RegisterRealMemoryCache();
            DefaultHttpContext context = this.MockRequest(200, "172.68.0.1");
            context.Request.Headers["CF-Connecting-IP"] = "198.51.100.23";

            INoBrute noBrute = new NoBrute.Data.NoBrute(this.provider.BuildServiceProvider());

            NoBruteRequestCheck attacker = null;
            for (int i = 0; i < 5; i++)
            {
                attacker = noBrute.CheckRequest("LOGIN");
            }

            context.Request.Headers["CF-Connecting-IP"] = "198.51.100.77";
            NoBruteRequestCheck honestCustomer = noBrute.CheckRequest("LOGIN");

            attacker.RemoteAddr.ShouldBe("198.51.100.23");
            attacker.IsGreenRequest.ShouldBeFalse();
            honestCustomer.RemoteAddr.ShouldBe("198.51.100.77");
            honestCustomer.IsGreenRequest.ShouldBeTrue();
        }

        #endregion Robustness Test Cases
    }
}
