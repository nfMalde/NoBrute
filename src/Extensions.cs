namespace Microsoft.Extensions.DependencyInjection
{
    public static class NoBruteExtensions
    {
        /// <summary>
        /// Adds NoBrute Services for BruteForce Protection
        /// </summary>
        /// <param name="services">The services.</param>
        public static void AddNoBrute(this IServiceCollection services)
        {
            services.AddScoped<NoBrute.NoBruteAttribute>();
            services.AddScoped<NoBrute.NoBrutePageFilter>();
            services.AddScoped<NoBrute.Domain.INoBrute, NoBrute.Data.NoBrute>();
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
            var filter = new NoBrute.NoBruteEndpointFilter(requestName, autoProcess);
            return builder.AddEndpointFilter((context, next) => filter.InvokeAsync(context, next));
        }
    }
}