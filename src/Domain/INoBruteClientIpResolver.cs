using Microsoft.AspNetCore.Http;

namespace NoBrute.Domain
{
    /// <summary>
    /// Resolves the client IP address a NoBrute cache entry is stored for.
    /// Register your own implementation before calling <c>AddNoBrute()</c> to override the default behaviour.
    /// </summary>
    public interface INoBruteClientIpResolver
    {
        /// <summary>
        /// Resolves the client IP address for the given request.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>The client IP address, or <c>"unknown"</c> if it cannot be determined.</returns>
        string ResolveClientIp(HttpContext context);
    }
}
