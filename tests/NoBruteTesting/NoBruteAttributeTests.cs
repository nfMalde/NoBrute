using Moq;
using NoBrute.Domain; 
using Shouldly;
using System.Diagnostics;
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
        /// It should increase request time if no green request.
        /// </summary>
        [Fact]
        public void ItShouldIncreaseRequestTimeIfNoGreenRequest()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST");
            int increaseMS = 50;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            Stopwatch sw = Stopwatch.StartNew();
            attribute.OnActionExecuting(this.GetActionExecutingContextMock());
            sw.Stop();

            sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(increaseMS - 5); // small tolerance for timer resolution
        }

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

            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST", true);
            attribute.OnActionExecuting(this.GetActionExecutingContextMock());

            attribute.OnActionExecuted(this.GetActionExecutedContextMock(expectedStatusCode));

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
