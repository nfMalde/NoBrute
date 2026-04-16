using Moq;
using NoBrute.Domain;
using Shouldly;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace NoBruteTesting
{
    /// <summary>
    /// Tests for the NoBrute Endpoint Filter (Minimal API)
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.NoBruteEndpointFilterTestCasesAbstract" />
    public class NoBruteEndpointFilterTests : Abstracts.NoBruteEndpointFilterTestCasesAbstract
    {
        /// <summary>
        /// It should increase request time if no green request.
        /// </summary>
        [Fact]
        public async Task ItShouldIncreaseRequestTimeIfNoGreenRequest()
        {
            NoBrute.NoBruteEndpointFilter filter = new NoBrute.NoBruteEndpointFilter("FALSY_REQUEST");
            const int increaseMS = 60;
            const int timingToleranceMS = 10;
            Stopwatch stopwatch = Stopwatch.StartNew();
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            await filter.InvokeAsync(this.GetEndpointFilterInvocationContext(), this.GetEndpointFilterDelegate());

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
        public async Task ItShouldHandleAutoClearForCorrectStatusCode(int expectedStatusCode, bool expectedAutoclear)
        {
            int increaseMS = 1000;
            Mock<INoBrute> mock = this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            NoBrute.NoBruteEndpointFilter filter = new NoBrute.NoBruteEndpointFilter("FALSY_REQUEST", true);

            await filter.InvokeAsync(
                this.GetEndpointFilterInvocationContext(),
                this.GetEndpointFilterDelegate(expectedStatusCode));

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
