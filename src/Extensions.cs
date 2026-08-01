namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using NoBrute.Data;
    using NoBrute.Domain;
    using NoBrute.Models;
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
        public static void AddNoBrute(this IServiceCollection services, Action<NoBruteRegistrationOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new NoBruteRegistrationOptions();
            configure(options);

            services.AddHttpContextAccessor();
            services.TryAddSingleton(options);

            // Registered with TryAdd so applications can plug in their own implementation
            // by registering it before calling AddNoBrute().
            services.TryAddSingleton<INoBruteClientIpResolver>(provider => new NoBruteClientIpResolver(provider, options.ClientIp));
            services.TryAddSingleton<INoBruteEntryLimiter>(provider => new NoBruteEntryLimiter(provider, options.MaxTrackedEntries));

            services.AddScoped<INoBrute, global::NoBrute.Data.NoBrute>();

            if (options.UseMvc)
            {
                services.AddScoped<global::NoBrute.NoBruteAttribute>();
            }

            if (options.UseRazorPages)
            {
                services.AddScoped<global::NoBrute.NoBrutePageFilter>();
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
