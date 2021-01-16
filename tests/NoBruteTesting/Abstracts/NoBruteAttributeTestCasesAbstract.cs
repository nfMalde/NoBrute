using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoBrute.Domain;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NoBruteTesting.Abstracts
{
    /// <summary>
    /// TestCase Abstract class for NoBruteAttribute Tests
    /// </summary>
    /// <seealso cref="NoBruteTesting.Abstracts.Base.TestCaseAbstractBase" />
    public abstract class NoBruteAttributeTestCasesAbstract: Base.TestCaseAbstractBase
    {

        /// <summary>
        /// Gets the action execution delegate.
        /// </summary>
        /// <returns></returns>
        protected ActionExecutionDelegate GetActionExecutionDelegate()
        {
            ActionExecutionDelegate next = () => {
                var ctx = new ActionExecutedContext(this.GetActionContextMock(), new List<IFilterMetadata>(), Mock.Of<ControllerBase>());
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
            Mock<NoBrute.Domain.INoBrute> mock = new Mock<NoBrute.Domain.INoBrute>();

            NoBrute.Models.NoBruteRequestCheck check = new NoBrute.Models.NoBruteRequestCheck();
            check.AppendRequestTime = increaseTime;
            check.IsGreenRequest = greenRequest;
            check.RemoteAddr = ip;

            mock.Setup(x => x.CheckRequest(It.IsAny<string>())).Returns(check);


            this.provider.AddScoped<INoBrute>(f => mock.Object);

            return mock;
        }

        /// <summary>
        /// Gets the action context mock.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="modelState">State of the model.</param>
        /// <returns></returns>
        protected ActionContext GetActionContextMock(HttpContext context = null, ModelStateDictionary modelState = null)
        {
            ActionContext actionContext = new ActionContext(
                                context ?? Mock.Of<HttpContext>(),
                                Mock.Of<RouteData>(),
                                Mock.Of<ActionDescriptor>(),
                                modelState ?? new ModelStateDictionary()
                            );


            return actionContext;
        }

        /// <summary>
        /// Gets the action executing context mock.
        /// </summary>
        /// <returns></returns>
        protected ActionExecutingContext GetActionExecutingContextMock()
        {
            ModelStateDictionary modelState = new ModelStateDictionary();

            DefaultHttpContext http = new DefaultHttpContext();
            http.RequestServices = this.provider.BuildServiceProvider();

            ActionExecutingContext actionExecutingContext = new ActionExecutingContext(
                this.GetActionContextMock(http, modelState),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<ControllerBase>()
            );

            return actionExecutingContext;
        }

        /// <summary>
        /// Gets the action executed context mock.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <returns></returns>
        protected ActionExecutedContext GetActionExecutedContextMock(int statusCode = 200)
        {
            ModelStateDictionary modelState = new ModelStateDictionary();

            DefaultHttpContext http = new DefaultHttpContext();
            http.RequestServices = this.provider.BuildServiceProvider();
            http.Response.StatusCode = statusCode;

            ActionExecutedContext actionExecutedContext = new ActionExecutedContext(
                this.GetActionContextMock(http, modelState),
                new List<IFilterMetadata>(),
                Mock.Of<ControllerBase>());

            return actionExecutedContext;
        }
    }
}
