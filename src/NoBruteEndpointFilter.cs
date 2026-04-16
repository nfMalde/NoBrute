using Microsoft.AspNetCore.Http;
using NoBrute.Domain;
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
            var check = service?.CheckRequest(requestName);

            if (check?.IsGreenRequest == false)
            {
                await Task.Delay(check.AppendRequestTime, context.HttpContext.RequestAborted);
            }

            var result = await next(context);

            if (autoProcess)
            {
                service?.AutoProcessRequestRelease(context.HttpContext.Response.StatusCode, requestName);
            }

            return result;
        }
    }
}
