using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoBrute.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoBruteTesting.Abstracts
{
    /// <summary>
    /// TestCase Abstract class for NoBrutePageFilter Tests
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.Base.TestCaseAbstractBase" />
    public abstract class NoBrutePageFilterTestCasesAbstract : Base.TestCaseAbstractBase
    {
        /// <summary>
        /// Gets the page handler execution delegate.
        /// </summary>
        /// <returns></returns>
        protected PageHandlerExecutionDelegate GetPageHandlerExecutionDelegate()
        {
            PageHandlerExecutionDelegate next = () =>
            {
                var pageContext = new PageContext(
                    new ActionContext(
                        Mock.Of<HttpContext>(),
                        Mock.Of<RouteData>(),
                        Mock.Of<ActionDescriptor>(),
                        new ModelStateDictionary()));

                var ctx = new PageHandlerExecutedContext(
                    pageContext,
                    new List<IFilterMetadata>(),
                    new HandlerMethodDescriptor(),
                    new object());

                return Task.FromResult(ctx);
            };

            return next;
        }

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
        /// Gets the page handler executing context mock.
        /// </summary>
        /// <returns></returns>
        protected PageHandlerExecutingContext GetPageHandlerExecutingContextMock()
        {
            ModelStateDictionary modelState = new ModelStateDictionary();

            DefaultHttpContext http = new DefaultHttpContext();
            http.RequestServices = this.provider.BuildServiceProvider();

            PageContext pageContext = new PageContext(
                new ActionContext(
                    http,
                    Mock.Of<RouteData>(),
                    Mock.Of<ActionDescriptor>(),
                    modelState));

            PageHandlerExecutingContext pageHandlerExecutingContext = new PageHandlerExecutingContext(
                pageContext,
                new List<IFilterMetadata>(),
                new HandlerMethodDescriptor(),
                new Dictionary<string, object>(),
                new object());

            return pageHandlerExecutingContext;
        }

        /// <summary>
        /// Gets the page handler executed context mock.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <returns></returns>
        protected PageHandlerExecutedContext GetPageHandlerExecutedContextMock(int statusCode = 200)
        {
            ModelStateDictionary modelState = new ModelStateDictionary();

            DefaultHttpContext http = new DefaultHttpContext();
            http.RequestServices = this.provider.BuildServiceProvider();
            http.Response.StatusCode = statusCode;

            PageContext pageContext = new PageContext(
                new ActionContext(
                    http,
                    Mock.Of<RouteData>(),
                    Mock.Of<ActionDescriptor>(),
                    modelState));

            PageHandlerExecutedContext pageHandlerExecutedContext = new PageHandlerExecutedContext(
                pageContext,
                new List<IFilterMetadata>(),
                new HandlerMethodDescriptor(),
                new object());

            return pageHandlerExecutedContext;
        }
    }
}
