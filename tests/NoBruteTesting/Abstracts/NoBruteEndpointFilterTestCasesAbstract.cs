using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoBrute.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoBruteTesting.Abstracts
{
    /// <summary>
    /// TestCase Abstract class for NoBruteEndpointFilter Tests
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.Base.TestCaseAbstractBase" />
    public abstract class NoBruteEndpointFilterTestCasesAbstract : Base.TestCaseAbstractBase
    {
        /// <summary>
        /// Registers the no brute service mock.
        /// </summary>
        /// <param name="greenRequest">if set to <c>true</c> [green request].</param>
        /// <param name="increaseTime">The increase time.</param>
        /// <param name="ip">The ip.</param>
        /// <returns></returns>
        protected Mock<INoBrute> RegisterNoBruteServiceMock(bool greenRequest, int increaseTime, string ip)
        {
            Mock<INoBrute> mock = new Mock<INoBrute>();

            NoBrute.Models.NoBruteRequestCheck check = new NoBrute.Models.NoBruteRequestCheck();
            check.AppendRequestTime = increaseTime;
            check.IsGreenRequest = greenRequest;
            check.RemoteAddr = ip;

            mock.Setup(x => x.CheckRequest(It.IsAny<string>())).Returns(check);

            this.provider.AddScoped<INoBrute>(f => mock.Object);

            return mock;
        }

        /// <summary>
        /// Gets a mocked EndpointFilterInvocationContext.
        /// </summary>
        /// <returns></returns>
        protected EndpointFilterInvocationContext GetEndpointFilterInvocationContext()
        {
            DefaultHttpContext http = new DefaultHttpContext();
            http.RequestServices = this.provider.BuildServiceProvider();

            Mock<EndpointFilterInvocationContext> mock = new Mock<EndpointFilterInvocationContext>();
            mock.Setup(x => x.HttpContext).Returns(http);
            mock.Setup(x => x.Arguments).Returns(new List<object>());

            return mock.Object;
        }

        /// <summary>
        /// Gets an EndpointFilterDelegate that sets the given status code on the response.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <returns></returns>
        protected EndpointFilterDelegate GetEndpointFilterDelegate(int statusCode = 200)
        {
            return (context) =>
            {
                context.HttpContext.Response.StatusCode = statusCode;
                return new ValueTask<object>(new object());
            };
        }
    }
}
