using Moq;
using NoBrute.Domain;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

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
        [Test]
        public void ItShouldIncreaseRequestTimeIfNoGreenRequest()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST");
            int increaseMS = 1000;
            // Save Time
            double ms = DateTime.Now.TimeOfDay.TotalMilliseconds;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");
            attribute.OnActionExecuting(this.GetActionExecutingContextMock());

            double ms2 = DateTime.Now.TimeOfDay.TotalMilliseconds;

            Assert.GreaterOrEqual((ms2 - ms), increaseMS); // We expect that that the request was delayed by 1000ms
        }

        /// <summary>
        /// It should increase request time if no green request in asynchronous mode.
        /// </summary>
        [Test]
        public async Task ItShouldIncreaseRequestTimeIfNoGreenRequestAsync()
        {
            NoBrute.NoBruteAttribute attribute = new NoBrute.NoBruteAttribute("FALSY_REQUEST");
            int increaseMS = 1000;
            // Save Time
            double ms = DateTime.Now.TimeOfDay.TotalMilliseconds;
            this.RegisterNoBruteServiceMock(false, increaseMS, "127.0.1");

            await attribute.OnActionExecutionAsync(this.GetActionExecutingContextMock(), this.GetActionExecutionDelegate());

            double ms2 = DateTime.Now.TimeOfDay.TotalMilliseconds;

            Assert.GreaterOrEqual((ms2 - ms), increaseMS); // We expect that that the request was delayed by 1000ms
        }

        /// <summary>
        /// It should handle automatic clear for correct status code.
        /// </summary>
        /// <param name="expectedStatusCode">The expected status code.</param>
        /// <param name="expectedAutoclear">if set to <c>true</c> [expected autoclear].</param>
        [TestCase(200, true)]
        public void ItShouldHandleAutoClearForCorrectStatusCode(int expectedStatusCode, bool expectedAutoclear)
        {
            int increaseMS = 1000;
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