using Moq;
using NoBrute.Domain;
using Shouldly;
using System;
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
        /// It should increase request time if no green request.
        /// </summary>
        [Fact]
        public void ItShouldIncreaseRequestTimeIfNoGreenRequest()
        {
            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            int increaseMS = 1000;
            // Save Time
            double ms = DateTime.Now.TimeOfDay.TotalMilliseconds;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            filter.OnPageHandlerExecuting(this.GetPageHandlerExecutingContextMock());

            double ms2 = DateTime.Now.TimeOfDay.TotalMilliseconds;

            (ms2 - ms).ShouldBeGreaterThanOrEqualTo(increaseMS); // We expect that the request was delayed by 1000ms
        }

        /// <summary>
        /// It should increase request time if no green request in asynchronous mode.
        /// </summary>
        [Fact]
        public async Task ItShouldIncreaseRequestTimeIfNoGreenRequestAsync()
        {
            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            int increaseMS = 1000;
            // Save Time
            double ms = DateTime.Now.TimeOfDay.TotalMilliseconds;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            await filter.OnPageHandlerExecutionAsync(this.GetPageHandlerExecutingContextMock(), this.GetPageHandlerExecutionDelegate());

            double ms2 = DateTime.Now.TimeOfDay.TotalMilliseconds;

            (ms2 - ms).ShouldBeGreaterThanOrEqualTo(increaseMS); // We expect that the request was delayed by 1000ms
        }

        /// <summary>
        /// It should handle automatic clear for correct status code.
        /// </summary>
        /// <param name="expectedStatusCode">The expected status code.</param>
        /// <param name="expectedAutoclear">if set to <c>true</c> [expected autoclear].</param>
        [Theory]
        [InlineData(200, true)]
        public void ItShouldHandleAutoClearForCorrectStatusCode(int expectedStatusCode, bool expectedAutoclear)
        {
            int increaseMS = 1000;
            Mock<INoBrute> mock = this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST", true);
            filter.OnPageHandlerExecuting(this.GetPageHandlerExecutingContextMock());

            filter.OnPageHandlerExecuted(this.GetPageHandlerExecutedContextMock(expectedStatusCode));

            if (expectedAutoclear)
            {
                mock.Verify(x =>
                x.AutoProcessRequestRelease(expectedStatusCode, "FALSY_REQUEST"),
                Times.Once()
                );
            }
            else
            {
                mock.Verify(
                    x => x.AutoProcessRequestRelease(
                        It.IsAny<int>(),
                        It.IsAny<string>()),
                    Times.Never()
                    );
            }
        }
    }
}
