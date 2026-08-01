using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NoBrute.Domain;
using NoBrute.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace NoBrute.Data
{
    /// <summary>
    /// Default <see cref="INoBruteClientIpResolver"/>.
    /// Returns the socket peer address unless forwarded headers are enabled and the peer is a trusted proxy.
    /// </summary>
    public class NoBruteClientIpResolver : INoBruteClientIpResolver
    {
        /// <summary>
        /// Value used when no IP address can be determined at all.
        /// </summary>
        public const string UnknownClientIp = "unknown";

        private readonly NoBruteClientIpOptions options;
        private readonly ILogger<NoBruteClientIpResolver> logger;
        private readonly IPAddress[] knownProxies;
        private readonly List<KeyValuePair<IPAddress, int>> knownNetworks;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteClientIpResolver"/> class using the
        /// <c>NoBrute:ClientIp</c> configuration section.
        /// </summary>
        /// <param name="provider">The service provider.</param>
        public NoBruteClientIpResolver(IServiceProvider provider)
            : this(provider, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoBruteClientIpResolver"/> class.
        /// </summary>
        /// <param name="provider">The service provider.</param>
        /// <param name="overrideOptions">Options set in code. If <c>null</c>, configuration is used.</param>
        public NoBruteClientIpResolver(IServiceProvider provider, NoBruteClientIpOptions overrideOptions)
        {
            this.logger = provider?.GetService<ILogger<NoBruteClientIpResolver>>();
            this.options = overrideOptions
                ?? NoBruteClientIpOptions.FromConfiguration(provider?.GetService<IConfiguration>()?.GetSection("NoBrute"));

            this.knownProxies = ParseProxies(this.options.KnownProxies);
            this.knownNetworks = ParseNetworks(this.options.KnownNetworks);

            if (this.options.UseForwardedHeaders && this.knownProxies.Length == 0 && this.knownNetworks.Count == 0)
            {
                this.logger?.LogWarning(
                    "NoBrute trusts forwarded client IP headers from every peer because neither NoBrute:ClientIp:KnownProxies nor NoBrute:ClientIp:KnownNetworks are configured. " +
                    "Anyone able to reach the application directly can spoof their client IP and bypass the protection.");
            }
        }

        /// <summary>
        /// Resolves the client IP address for the given request.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        public string ResolveClientIp(HttpContext context)
        {
            IPAddress remoteAddress = context?.Connection?.RemoteIpAddress;
            string peer = Format(remoteAddress);

            if (context == null || !this.options.UseForwardedHeaders || !this.IsTrustedProxy(remoteAddress))
            {
                return peer;
            }

            foreach (string header in this.options.Headers ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                if (!context.Request.Headers.TryGetValue(header, out StringValues values))
                {
                    continue;
                }

                IPAddress forwarded = this.SelectForwardedAddress(values);

                if (forwarded != null)
                {
                    return Format(forwarded);
                }
            }

            return peer;
        }

        /// <summary>
        /// Picks the address the trusted proxy chain reported for the client.
        /// </summary>
        /// <param name="values">The raw header values.</param>
        private IPAddress SelectForwardedAddress(StringValues values)
        {
            List<IPAddress> candidates = new List<IPAddress>();

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                foreach (string part in value.Split(','))
                {
                    if (TryParseAddress(part, out IPAddress address))
                    {
                        candidates.Add(address);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            int limit = this.options.ForwardLimit < 1 ? 1 : this.options.ForwardLimit;
            int index = candidates.Count - limit;

            return candidates[index < 0 ? 0 : index];
        }

        /// <summary>
        /// Determines whether forwarded headers of the given peer are trusted.
        /// </summary>
        /// <param name="address">The peer address.</param>
        private bool IsTrustedProxy(IPAddress address)
        {
            if (address == null)
            {
                return false;
            }

            IPAddress normalized = Normalize(address);

            if (this.options.TrustLoopback && IPAddress.IsLoopback(normalized))
            {
                return true;
            }

            // No explicit trust configuration: accept every peer (a warning was logged during startup).
            if (this.knownProxies.Length == 0 && this.knownNetworks.Count == 0)
            {
                return true;
            }

            if (this.knownProxies.Any(proxy => proxy.Equals(normalized)))
            {
                return true;
            }

            return this.knownNetworks.Any(network => IsInNetwork(normalized, network.Key, network.Value));
        }

        private static IPAddress[] ParseProxies(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(value => TryParseAddress(value, out IPAddress address) ? address : null)
                .Where(address => address != null)
                .ToArray();
        }

        private static List<KeyValuePair<IPAddress, int>> ParseNetworks(IEnumerable<string> values)
        {
            List<KeyValuePair<IPAddress, int>> networks = new List<KeyValuePair<IPAddress, int>>();

            foreach (string value in values ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string[] parts = value.Split('/');

                if (parts.Length != 2
                    || !IPAddress.TryParse(parts[0].Trim(), out IPAddress prefix)
                    || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int prefixLength))
                {
                    continue;
                }

                int maxLength = prefix.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;

                if (prefixLength < 0 || prefixLength > maxLength)
                {
                    continue;
                }

                networks.Add(new KeyValuePair<IPAddress, int>(Normalize(prefix), prefixLength));
            }

            return networks;
        }

        private static bool IsInNetwork(IPAddress address, IPAddress prefix, int prefixLength)
        {
            if (address.AddressFamily != prefix.AddressFamily)
            {
                return false;
            }

            if (prefixLength <= 0)
            {
                return true;
            }

            byte[] addressBytes = address.GetAddressBytes();
            byte[] prefixBytes = prefix.GetAddressBytes();

            if (prefixLength > prefixBytes.Length * 8)
            {
                return false;
            }

            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            for (int i = 0; i < fullBytes; i++)
            {
                if (addressBytes[i] != prefixBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits == 0)
            {
                return true;
            }

            int mask = (0xFF << (8 - remainingBits)) & 0xFF;

            return (addressBytes[fullBytes] & mask) == (prefixBytes[fullBytes] & mask);
        }

        private static bool TryParseAddress(string value, out IPAddress address)
        {
            address = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();

            if (IPAddress.TryParse(trimmed, out address))
            {
                address = Normalize(address);
                return true;
            }

            // Some proxies append the source port ("1.2.3.4:56789" / "[::1]:56789").
            if (IPEndPoint.TryParse(trimmed, out IPEndPoint endpoint))
            {
                address = Normalize(endpoint.Address);
                return true;
            }

            return false;
        }

        private static IPAddress Normalize(IPAddress address)
        {
            return address != null && address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        private static string Format(IPAddress address)
        {
            return address == null ? UnknownClientIp : Normalize(address).ToString();
        }
    }
}
