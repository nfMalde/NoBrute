using Microsoft.AspNetCore.Mvc.Filters;
using NoBrute.Domain;
using NoBrute.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
namespace NoBrute
{
    /// <summary>
    /// NoBruteAttribute
    /// Protects the given Action against brute force attacks
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Mvc.Filters.ActionFilterAttribute" />
    public class NoBruteAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// The request name
        /// </summary>
        private string requestName = null;

        /// <summary>
        /// The automatic process flag. If true, request delay will be cleared automatically for set status coces (Configurable)
        /// </summary>
        /// <remarks>
        /// Configurable via NoBrute->StatusCodesForAutoProcess  
        /// </remarks>
        private bool autoProcess = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteAttribute"/> class.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        public NoBruteAttribute(string requestName)
        {
            this.requestName = requestName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteAttribute"/> class.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="autoProcess">if set to <c>true</c> [automatic process].</param>
        public NoBruteAttribute(string requestName, bool autoProcess) : this(requestName)
        {
            this.autoProcess = autoProcess;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteAttribute"/> class.
        /// </summary>
        public NoBruteAttribute()
        {

        }

        #region Before Request
        /// <summary>
        /// </summary>
        /// <param name="context"></param>
        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            INoBrute service = (INoBrute)context.HttpContext.RequestServices.GetService(typeof(INoBrute));

            NoBruteRequestCheck check = service.CheckRequest(this.requestName);

            if (check != null && !check.IsGreenRequest)
            {
                System.Threading.Thread.Sleep(check.AppendRequestTime);
            }

        }

        /// <summary>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <inheritdoc />
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            INoBrute service = (INoBrute)context.HttpContext.RequestServices.GetService(typeof(INoBrute));

            NoBruteRequestCheck check = service.CheckRequest(this.requestName);

            if (check != null && !check.IsGreenRequest)
            {
                System.Threading.Thread.Sleep(check.AppendRequestTime);
            }

            await next();

            if (this.autoProcess)
            {
                service.AutoProcessRequestRelease(context.HttpContext.Response.StatusCode, this.requestName);
            }
        }

        #endregion BeforRequest

        #region AfterRequest

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (this.autoProcess)
            {
                INoBrute service = (INoBrute)context.HttpContext.RequestServices.GetService(typeof(INoBrute));

                service.AutoProcessRequestRelease(context.HttpContext.Response.StatusCode, this.requestName);
            }

        }



        #endregion BeforeRequest

    }
}
