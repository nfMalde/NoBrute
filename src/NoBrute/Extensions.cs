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
            services.AddScoped<NoBrute.Domain.INoBrute, NoBrute.Data.NoBrute>();
        }
    }
}