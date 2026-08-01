using Microsoft.Extensions.Configuration;
using NoBrute.Internal;
using System.Collections.Generic;

namespace NoBrute.Models
{
    /// <summary>
    /// Controls how NoBrute determines the client IP address a cache entry is stored for.
    /// Required when the application runs behind Cloudflare, a load balancer or any other reverse proxy,
    /// because in that case <c>HttpContext.Connection.RemoteIpAddress</c> is the proxy, not the client.
    /// </summary>
    public class NoBruteClientIpOptions
    {
        /// <summary>
        /// Default headers that are inspected when <see cref="UseForwardedHeaders"/> is enabled, in order.
        /// </summary>
        public static readonly string[] DefaultHeaders = { "CF-Connecting-IP", "X-Forwarded-For" };

        /// <summary>
        /// Gets or sets a value indicating whether forwarded headers (see <see cref="Headers"/>) are used
        /// to determine the client IP. Default is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Keep this disabled unless the application really is behind a proxy. Forwarded headers are
        /// attacker controlled: if they are trusted while the application is reachable directly, a bot can
        /// send a random value per request and never hit the same cache entry twice.
        /// </remarks>
        public bool UseForwardedHeaders { get; set; }

        /// <summary>
        /// Gets or sets the headers that are inspected, in order. The first header that contains a
        /// parsable IP address wins. Default is <c>CF-Connecting-IP</c> followed by <c>X-Forwarded-For</c>.
        /// </summary>
        public IList<string> Headers { get; set; } = new List<string>(DefaultHeaders);

        /// <summary>
        /// Gets or sets the number of proxies between the client and the server. For a header
        /// containing a chain of addresses (<c>client, proxy1, proxy2</c>) the entry
        /// <c>ForwardLimit</c> positions from the right is used. Default is <c>1</c>
        /// (a single proxy such as Cloudflare).
        /// </summary>
        public int ForwardLimit { get; set; } = 1;

        /// <summary>
        /// Gets or sets the IP addresses of proxies whose forwarded headers are trusted.
        /// If both this and <see cref="KnownNetworks"/> are empty, forwarded headers are accepted from
        /// every peer and a warning is logged during startup.
        /// </summary>
        public IList<string> KnownProxies { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the CIDR networks (e.g. <c>173.245.48.0/20</c>) whose forwarded headers are trusted.
        /// </summary>
        public IList<string> KnownNetworks { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether forwarded headers sent from loopback addresses are
        /// always trusted. Default is <c>true</c>, which keeps local development and integration tests working.
        /// </summary>
        public bool TrustLoopback { get; set; } = true;

        /// <summary>
        /// Builds the options from the <c>NoBrute:ClientIp</c> configuration section.
        /// </summary>
        /// <param name="noBruteSection">The <c>NoBrute</c> configuration section. May be <c>null</c>.</param>
        public static NoBruteClientIpOptions FromConfiguration(IConfiguration noBruteSection)
        {
            NoBruteClientIpOptions options = new NoBruteClientIpOptions();
            IConfigurationSection section = noBruteSection?.GetSection("ClientIp");

            if (section == null)
            {
                return options;
            }

            options.UseForwardedHeaders = ConfigReader.Read(section, "UseForwardedHeaders", options.UseForwardedHeaders);
            options.ForwardLimit = ConfigReader.Read(section, "ForwardLimit", options.ForwardLimit);
            options.TrustLoopback = ConfigReader.Read(section, "TrustLoopback", options.TrustLoopback);
            options.Headers = ConfigReader.ReadStringList(section, "Headers", options.Headers);
            options.KnownProxies = ConfigReader.ReadStringList(section, "KnownProxies", options.KnownProxies);
            options.KnownNetworks = ConfigReader.ReadStringList(section, "KnownNetworks", options.KnownNetworks);

            return options;
        }
    }
}
