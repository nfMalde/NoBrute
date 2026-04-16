namespace Microsoft.Extensions.DependencyInjection
{
    using System;

    public static class NoBruteExtensions
    {
        /// <summary>
        /// Adds NoBrute Services for BruteForce Protection with default options (MVC only).
        /// </summary>
        /// <param name="services">The services.</param>
        public static void AddNoBrute(this IServiceCollection services)
        {
            services.AddNoBrute(_ => { });
        }

        /// <summary>
        /// Adds NoBrute Services for BruteForce Protection with configurable options.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="configure">Action to configure registration options.</param>
        public static void AddNoBrute(this IServiceCollection services, Action<NoBrute.Models.NoBruteRegistrationOptions> configure)
        {
            var options = new NoBrute.Models.NoBruteRegistrationOptions();
            configure(options);

            services.AddScoped<NoBrute.Domain.INoBrute, NoBrute.Data.NoBrute>();

            if (options.UseMvc)
            {
                services.AddScoped<NoBrute.NoBruteAttribute>();
            }

            if (options.UseRazorPages)
            {
                services.AddScoped<NoBrute.NoBrutePageFilter>();
            }
        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    using Microsoft.AspNetCore.Http;

    public static class NoBruteEndpointExtensions
    {
        /// <summary>
        /// Adds NoBrute brute force protection to a Minimal API endpoint.
        /// </summary>
        /// <param name="builder">The route handler builder.</param>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="autoProcess">if set to <c>true</c>, automatically releases the request on configured status codes.</param>
        public static RouteHandlerBuilder WithNoBrute(this RouteHandlerBuilder builder, string requestName = null, bool autoProcess = true)
        {
            var normalizedRequestName = string.IsNullOrWhiteSpace(requestName) ? null : requestName;
            var filter = new NoBrute.NoBruteEndpointFilter(normalizedRequestName, autoProcess);
            return builder.AddEndpointFilter((context, next) => filter.InvokeAsync(context, next));
        }
    }
}