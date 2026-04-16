using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoBrute.Domain;
using System.Threading.Tasks;

namespace NoBrute
{
    /// <summary>
    /// NoBrutePageFilter
    /// Protects Razor Pages against brute force attacks
    /// </summary>
    public class NoBrutePageFilter : IPageFilter, IAsyncPageFilter
    {
        private readonly string requestName;
        private readonly bool autoProcess;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBrutePageFilter"/> class.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="autoProcess">if set to <c>true</c> [automatic process].</param>
        public NoBrutePageFilter(string requestName = null, bool autoProcess = true)
        {
            this.requestName = requestName;
            this.autoProcess = autoProcess;
        }

        /// <summary>
        /// Called when a handler is selected, before model binding.
        /// </summary>
        /// <param name="context">The context.</param>
        public void OnPageHandlerSelected(PageHandlerSelectedContext context) { }

        /// <summary>
        /// Called before the handler method executes.
        /// </summary>
        /// <param name="context">The context.</param>
        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            var service = context.HttpContext.RequestServices.GetService(typeof(INoBrute)) as INoBrute;
            var check = service?.CheckRequest(requestName);

            if (check?.IsGreenRequest == false)
            {
                System.Threading.Thread.Sleep(check.AppendRequestTime);
            }
        }

        /// <summary>
        /// Called after the handler method executes.
        /// </summary>
        /// <param name="context">The context.</param>
        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            if (autoProcess)
            {
                var service = context.HttpContext.RequestServices.GetService(typeof(INoBrute)) as INoBrute;
                service?.AutoProcessRequestRelease(context.HttpContext.Response.StatusCode, requestName);
            }
        }

        /// <summary>
        /// Called asynchronously when a handler is selected, before model binding.
        /// </summary>
        /// <param name="context">The context.</param>
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

        /// <summary>
        /// Called asynchronously before and after the handler method executes.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="next">The next delegate.</param>
        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
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
    }
}
