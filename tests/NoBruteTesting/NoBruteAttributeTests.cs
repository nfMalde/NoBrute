using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
    /// Tests for the NoBrute Filter Attribute
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.NoBruteAttributeTestCasesAbstract" />
    public class NoBruteAttributeTests : Abstracts.NoBruteAttributeTestCasesAbstract
    {
        /// <summary>
        /// It should increase request time if no green request in asynchronous mode.
        /// </summary>
        [Fact]
        public async Task ItShouldIncreaseRequestTimeIfNoGreenRequestAsync()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST");
            int increaseMS = 50;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            Stopwatch sw = Stopwatch.StartNew();
            await attribute.OnActionExecutionAsync(this.GetActionExecutingContextMock(), this.GetActionExecutionDelegate());
            sw.Stop();

            sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(increaseMS - 5); // small tolerance for timer resolution
        }

        /// <summary>
        /// It should delay without blocking the calling thread, so the thread pool stays available under load.
        /// </summary>
        [Fact]
        public async Task ItShouldNotBlockTheCallingThreadWhileDelaying()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST");
            const int increaseMS = 300;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            Task filterTask = attribute.OnActionExecutionAsync(this.GetActionExecutingContextMock(), this.GetActionExecutionDelegate());

            // While the request is delayed the awaiting thread must be free again.
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

            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST");
            ActionExecutingContext context = this.GetActionExecutingContextMock();
            bool nextWasCalled = false;

            Stopwatch stopwatch = Stopwatch.StartNew();
            await attribute.OnActionExecutionAsync(context, () =>
            {
                nextWasCalled = true;
                return Task.FromResult(new ActionExecutedContext(this.GetActionContextMock(), new List<IFilterMetadata>(), Mock.Of<ControllerBase>()));
            });
            stopwatch.Stop();

            nextWasCalled.ShouldBeFalse();
            context.Result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(blockedStatusCode);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // blocked requests are rejected instantly, never delayed
        }

        /// <summary>
        /// It should not delay green requests at all.
        /// </summary>
        [Fact]
        public async Task ItShouldNotDelayGreenRequests()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("GREEN_REQUEST");
            this.RegisterNoBruteServiceMock(true, 5000, "127.0.1");

            Stopwatch stopwatch = Stopwatch.StartNew();
            await attribute.OnActionExecutionAsync(this.GetActionExecutingContextMock(), this.GetActionExecutionDelegate());
            stopwatch.Stop();

            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
        }

        /// <summary>
        /// It should let the request pass when NoBrute is not registered at all.
        /// </summary>
        [Fact]
        public async Task ItShouldPassThroughWithoutTheService()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("ANY_REQUEST");
            bool nextWasCalled = false;

            await attribute.OnActionExecutionAsync(this.GetActionExecutingContextMock(), () =>
            {
                nextWasCalled = true;
                return Task.FromResult(new ActionExecutedContext(this.GetActionContextMock(), new List<IFilterMetadata>(), Mock.Of<ControllerBase>()));
            });

            nextWasCalled.ShouldBeTrue();
        }

        /// <summary>
        /// The synchronous filter methods are no longer used: ASP.NET Core always prefers the
        /// asynchronous ones, and blocking a thread pool thread is exactly what we want to avoid.
        /// </summary>
        [Fact]
        public void ItShouldNotBlockOnTheSynchronousFilterMethods()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST");
            Mock<INoBrute> mock = this.RegisterNoBruteServiceMock(false, 5000, "127.0.1");

            Stopwatch stopwatch = Stopwatch.StartNew();
            attribute.OnActionExecuting(this.GetActionExecutingContextMock());
            attribute.OnActionExecuted(this.GetActionExecutedContextMock());
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

            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST", true);
            ActionExecutingContext context = this.GetActionExecutingContextMock();
            context.HttpContext.Response.StatusCode = expectedStatusCode;

            await attribute.OnActionExecutionAsync(context, this.GetActionExecutionDelegate());

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
