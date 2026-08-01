using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Moq;
using NoBrute.Domain;
using Shouldly;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoBruteTesting
{
    /// <summary>
    /// Tests for the NoBrute Page Filter (Razor Pages)
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.NoBrutePageFilterTestCasesAbstract" />
    public class NoBrutePageFilterTests : Abstracts.NoBrutePageFilterTestCasesAbstract
    {
        /// <summary>
        /// It should increase request time if no green request in asynchronous mode.
        /// </summary>
        [Fact]
        public async Task ItShouldIncreaseRequestTimeIfNoGreenRequestAsync()
        {
            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            const int increaseMS = 60;
            const int timingToleranceMS = 10;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            Stopwatch stopwatch = Stopwatch.StartNew();
            await filter.OnPageHandlerExecutionAsync(this.GetPageHandlerExecutingContextMock(), this.GetPageHandlerExecutionDelegate());
            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(increaseMS - timingToleranceMS);
        }

        /// <summary>
        /// It should delay without blocking the calling thread, so the thread pool stays available under load.
        /// </summary>
        [Fact]
        public async Task ItShouldNotBlockTheCallingThreadWhileDelaying()
        {
            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            const int increaseMS = 300;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            Task filterTask = filter.OnPageHandlerExecutionAsync(this.GetPageHandlerExecutingContextMock(), this.GetPageHandlerExecutionDelegate());

            filterTask.IsCompleted.ShouldBeFalse();

            await filterTask;
        }

        /// <summary>
        /// It should short circuit with the blocked status code once the tracked entry limit is reached.
        /// </summary>
        [Fact]
        public async Task ItShouldHardBlockWhenEntryLimitIsReached()
        {
            const int blockedStatusCode = 429;
            this.RegisterNoBruteServiceMock(false, 5000, "127.0.1", blocked: true, blockedStatusCode: blockedStatusCode);

            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            PageHandlerExecutingContext context = this.GetPageHandlerExecutingContextMock();
            bool nextWasCalled = false;

            await filter.OnPageHandlerExecutionAsync(context, () =>
            {
                nextWasCalled = true;

                PageContext pageContext = new PageContext(context);

                return Task.FromResult(new PageHandlerExecutedContext(
                    pageContext,
                    new List<IFilterMetadata>(),
                    new HandlerMethodDescriptor(),
                    new object()));
            });

            nextWasCalled.ShouldBeFalse();
            context.Result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(blockedStatusCode);
        }

        /// <summary>
        /// The synchronous filter methods are no longer used: Razor Pages always prefers the
        /// asynchronous ones, and blocking a thread pool thread is exactly what we want to avoid.
        /// </summary>
        [Fact]
        public void ItShouldNotBlockOnTheSynchronousFilterMethods()
        {
            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            Mock<INoBrute> mock = this.RegisterNoBruteServiceMock(false, 5000, "127.0.1");

            Stopwatch stopwatch = Stopwatch.StartNew();
            filter.OnPageHandlerExecuting(this.GetPageHandlerExecutingContextMock());
            filter.OnPageHandlerExecuted(this.GetPageHandlerExecutedContextMock());
            stopwatch.Stop();

            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
            mock.Verify(x => x.CheckRequest(It.IsAny<string>()), Times.Never());
        }

        /// <summary>
        /// It should handle automatic clear for correct status code.
        /// </summary>
        /// <param name="expectedStatusCode">The expected status code.</param>
        /// <param name="expectedAutoclear">if set to <c>true</c> [expected autoclear].</param>
        [Theory]
        [InlineData(200, true)]
        public async Task ItShouldHandleAutoClearForCorrectStatusCode(int expectedStatusCode, bool expectedAutoclear)
        {
            int increaseMS = 50;
            Mock<INoBrute> mock = this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST", true);
            PageHandlerExecutingContext context = this.GetPageHandlerExecutingContextMock();
            context.HttpContext.Response.StatusCode = expectedStatusCode;

            await filter.OnPageHandlerExecutionAsync(context, this.GetPageHandlerExecutionDelegate());

            if (expectedAutoclear)
            {
                mock.Verify(x =>
                x.AutoProcessRequestReleaseAsync(expectedStatusCode, "FALSY_REQUEST", It.IsAny<CancellationToken>()),
                Times.Once()
                );
            }
            else
            {
                mock.Verify(
                    x => x.AutoProcessRequestReleaseAsync(
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never()
                    );
            }
        }
    }
}
