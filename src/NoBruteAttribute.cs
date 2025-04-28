using Microsoft.AspNetCore.Mvc.Filters;
using NoBrute.Domain;
using NoBrute.Models;
using System.Threading.Tasks;

namespace NoBrute
{
    /// <summary>
    /// NoBruteAttribute
    /// Protects the given Action against brute force attacks
    /// </summary>
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
            this.requestName = requestName;
            this.autoProcess = autoProcess;
        }

        /// <summary>
        /// Executes before the action.
        /// </summary>
        /// <param name="context"></param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var service = context.HttpContext.RequestServices.GetService(typeof(INoBrute)) as INoBrute;
            var check = service?.CheckRequest(requestName);

            if (check?.IsGreenRequest == false)
            {
                System.Threading.Thread.Sleep(check.AppendRequestTime);
            }
        }

        /// <summary>
        /// Executes asynchronously before and after the action.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var service = context.HttpContext.RequestServices.GetService(typeof(INoBrute)) as INoBrute;
            var check = service?.CheckRequest(requestName);

            if (check?.IsGreenRequest == false)
            {
                System.Threading.Thread.Sleep(check.AppendRequestTime);
            }

            await next();

            if (autoProcess)
            {
                service?.AutoProcessRequestRelease(context.HttpContext.Response.StatusCode, requestName);
            }
        }

        /// <summary>
        /// Executes after the action.
        /// </summary>
        /// <param name="context"></param>
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (autoProcess)
            {
                var service = context.HttpContext.RequestServices.GetService(typeof(INoBrute)) as INoBrute;
                service?.AutoProcessRequestRelease(context.HttpContext.Response.StatusCode, requestName);
            }
        }
    }
}
