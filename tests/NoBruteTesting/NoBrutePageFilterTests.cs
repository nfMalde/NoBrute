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
            int increaseMS = 50;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            Stopwatch sw = Stopwatch.StartNew();
            filter.OnPageHandlerExecuting(this.GetPageHandlerExecutingContextMock());
            sw.Stop();

            sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(increaseMS - 5); // small tolerance for timer resolution
        }

        /// <summary>
        /// It should increase request time if no green request in asynchronous mode.
        /// </summary>
        [Fact]
        public async Task ItShouldIncreaseRequestTimeIfNoGreenRequestAsync()
        {
            NoBrute.NoBrutePageFilter filter = new NoBrute.NoBrutePageFilter("FALSY_REQUEST");
            int increaseMS = 50;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            Stopwatch sw = Stopwatch.StartNew();
            await filter.OnPageHandlerExecutionAsync(this.GetPageHandlerExecutingContextMock(), this.GetPageHandlerExecutionDelegate());
            sw.Stop();

            sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(increaseMS - 5); // small tolerance for timer resolution
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
            int increaseMS = 50;
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
