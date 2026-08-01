using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NoBrute.Domain;
using NoBrute.Models;
using System.Threading;
using System.Threading.Tasks;

namespace NoBrute
{
    /// <summary>
    /// NoBruteAttribute
    /// Protects the given Action against brute force attacks
    /// </summary>
    /// <remarks>
    /// The whole work happens in <see cref="OnActionExecutionAsync"/>. MVC always prefers the
    /// asynchronous filter interface when a filter implements both, so the request is delayed with
    /// <c>await Task.Delay(...)</c> and never blocks a thread pool thread.
    /// </remarks>
    public class NoBruteAttribute : ActionFilterAttribute
    {
        private readonly string requestName;
        private readonly bool autoProcess;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteAttribute"/> class.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="autoProcess">if set to <c>true</c> [automatic process].</param>
        public NoBruteAttribute(string requestName = null, bool autoProcess = true)
        {
            this.requestName = string.IsNullOrWhiteSpace(requestName) ? null : requestName;
            this.autoProcess = autoProcess;
        }

        /// <summary>
        /// Executes asynchronously before and after the action.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var service = context.HttpContext.RequestServices.GetService(typeof(INoBrute)) as INoBrute;
            CancellationToken cancellationToken = context.HttpContext.RequestAborted;

            NoBruteRequestCheck check = service == null
                ? null
                : await service.CheckRequestAsync(requestName, cancellationToken);

            if (check != null && check.IsBlocked)
            {
                context.Result = new StatusCodeResult(check.BlockedStatusCode);
                return;
            }

            if (check?.IsGreenRequest == false && check.AppendRequestTime > 0)
            {
                await Task.Delay(check.AppendRequestTime, cancellationToken);
            }

            await next();

            if (autoProcess && service != null)
            {
                await service.AutoProcessRequestReleaseAsync(context.HttpContext.Response.StatusCode, requestName);
            }
        }
    }
}
