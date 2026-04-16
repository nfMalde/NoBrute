using Moq;
using NoBrute.Domain;
using Shouldly;
using System.Diagnostics;
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
            const int increaseMS = 60;
            const int timingToleranceMS = 10;
            Stopwatch stopwatch = Stopwatch.StartNew();
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            filter.OnPageHandlerExecuting(this.GetPageHandlerExecutingContextMock());

            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(increaseMS - timingToleranceMS);
        }

        /// <summary>
        /// It should increase request time if no green request in asynchronous mode.
        /// </summary>
        [Fact]
        public async Task ItShouldIncreaseRequestTimeIfNoGreenRequestAsync()
        {
            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            const int increaseMS = 60;
            const int timingToleranceMS = 10;
            Stopwatch stopwatch = Stopwatch.StartNew();
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            await filter.OnPageHandlerExecutionAsync(this.GetPageHandlerExecutingContextMock(), this.GetPageHandlerExecutionDelegate());

            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(increaseMS - timingToleranceMS);
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
