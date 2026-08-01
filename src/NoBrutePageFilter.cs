using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NoBrute.Domain;
using NoBrute.Models;
using System.Threading;
using System.Threading.Tasks;

namespace NoBrute
{
    /// <summary>
    /// NoBrutePageFilter
    /// Protects Razor Pages against brute force attacks
    /// </summary>
    /// <remarks>
    /// Only the asynchronous filter interface carries logic. Razor Pages always prefers
    /// <see cref="IAsyncPageFilter"/> over <see cref="IPageFilter"/>, so the request is delayed with
    /// <c>await Task.Delay(...)</c> and never blocks a thread pool thread.
    /// </remarks>
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
            this.requestName = string.IsNullOrWhiteSpace(requestName) ? null : requestName;
            this.autoProcess = autoProcess;
        }

        /// <summary>
        /// Called when a handler is selected, before model binding.
        /// </summary>
        /// <param name="context">The context.</param>
        public void OnPageHandlerSelected(PageHandlerSelectedContext context) { }

        /// <summary>
        /// Not used. See <see cref="OnPageHandlerExecutionAsync"/>.
        /// </summary>
        /// <param name="context">The context.</param>
        public void OnPageHandlerExecuting(PageHandlerExecutingContext context) { }

        /// <summary>
        /// Not used. See <see cref="OnPageHandlerExecutionAsync"/>.
        /// </summary>
        /// <param name="context">The context.</param>
        public void OnPageHandlerExecuted(PageHandlerExecutedContext context) { }

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
