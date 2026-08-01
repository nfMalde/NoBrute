using Microsoft.AspNetCore.Http;
using NoBrute.Domain;
using NoBrute.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NoBrute
{
    /// <summary>
    /// NoBruteEndpointFilter
    /// Protects Minimal API endpoints against brute force attacks
    /// </summary>
    public class NoBruteEndpointFilter : IEndpointFilter
    {
        private readonly string requestName;
        private readonly bool autoProcess;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteEndpointFilter"/> class.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="autoProcess">if set to <c>true</c> [automatic process].</param>
        public NoBruteEndpointFilter(string requestName = null, bool autoProcess = true)
        {
            this.requestName = string.IsNullOrWhiteSpace(requestName) ? null : requestName;
            this.autoProcess = autoProcess;
        }

        /// <summary>
        /// Invokes the endpoint filter, applying brute force protection.
        /// </summary>
        /// <param name="context">The endpoint filter invocation context.</param>
        /// <param name="next">The next filter or endpoint delegate.</param>
        public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var service = context.HttpContext.RequestServices.GetService(typeof(INoBrute)) as INoBrute;
            CancellationToken cancellationToken = context.HttpContext.RequestAborted;

            NoBruteRequestCheck check = service == null
                ? null
                : await service.CheckRequestAsync(requestName, cancellationToken);

            if (check != null && check.IsBlocked)
            {
                return Results.StatusCode(check.BlockedStatusCode);
            }

            if (check?.IsGreenRequest == false && check.AppendRequestTime > 0)
            {
                await Task.Delay(check.AppendRequestTime, cancellationToken);
            }

            var result = await next(context);

            if (!autoProcess)
            {
                return result;
            }

            var statusCode = TryGetStatusCode(result);
            if (statusCode.HasValue)
            {
                await ReleaseAsync(service, statusCode.Value);
                return result;
            }

            if (result is IResult httpResult)
            {
                return new AutoProcessingResult(httpResult, service, requestName);
            }

            await ReleaseAsync(service, context.HttpContext.Response.StatusCode);
            return result;
        }

        private Task ReleaseAsync(INoBrute service, int statusCode)
        {
            return service == null
                ? Task.CompletedTask
                : service.AutoProcessRequestReleaseAsync(statusCode, requestName);
        }

        private static int? TryGetStatusCode(object result)
        {
            return (result as IStatusCodeHttpResult)?.StatusCode;
        }

        private sealed class AutoProcessingResult : IResult
        {
            private readonly IResult innerResult;
            private readonly INoBrute service;
            private readonly string requestName;

            public AutoProcessingResult(IResult innerResult, INoBrute service, string requestName)
            {
                this.innerResult = innerResult ?? throw new ArgumentNullException(nameof(innerResult));
                this.service = service;
                this.requestName = requestName;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                await this.innerResult.ExecuteAsync(httpContext);

                if (this.service != null)
                {
                    await this.service.AutoProcessRequestReleaseAsync(httpContext.Response.StatusCode, this.requestName);
                }
            }
        }
    }
}
