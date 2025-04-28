using Microsoft.Extensions.DependencyInjection;
using NoBrute.Domain;
using NoBrute.Models;
using Shouldly;
using System;
using System.Collections.Generic;
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
    }
}
