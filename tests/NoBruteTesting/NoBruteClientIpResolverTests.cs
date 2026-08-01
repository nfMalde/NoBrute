using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using NoBrute.Data;
using NoBrute.Models;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace NoBruteTesting
{
    /// <summary>
    /// Tests for the client IP resolution behind reverse proxies (Cloudflare, load balancers, ...).
    /// </summary>
    public class NoBruteClientIpResolverTests
    {
        /// <summary>
        /// It should use the socket peer address when forwarded headers are disabled.
        /// </summary>
        [Fact]
        public void ItShouldUseThePeerAddressByDefault()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions());
            HttpContext context = CreateContext("203.0.113.7", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            resolver.ResolveClientIp(context).ShouldBe("203.0.113.7");
        }

        /// <summary>
        /// It should prefer the Cloudflare header over X-Forwarded-For.
        /// </summary>
        [Fact]
        public void ItShouldPreferTheCloudflareHeader()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true
            });

            HttpContext context = CreateContext("172.68.0.1", new Dictionary<string, string>
            {
                { "CF-Connecting-IP", "198.51.100.23" },
                { "X-Forwarded-For", "192.0.2.99" }
            });

            resolver.ResolveClientIp(context).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should honor the forward limit when the header contains a proxy chain.
        /// </summary>
        /// <param name="forwardedFor">The X-Forwarded-For value.</param>
        /// <param name="forwardLimit">The configured forward limit.</param>
        /// <param name="expected">The expected client IP.</param>
        [Theory]
        [InlineData("198.51.100.23", 1, "198.51.100.23")]
        [InlineData("198.51.100.23, 172.68.0.1", 1, "172.68.0.1")]
        [InlineData("198.51.100.23, 172.68.0.1", 2, "198.51.100.23")]
        [InlineData("198.51.100.23, 172.68.0.1", 5, "198.51.100.23")]
        [InlineData("198.51.100.23:44321", 1, "198.51.100.23")]
        public void ItShouldHonorTheForwardLimit(string forwardedFor, int forwardLimit, string expected)
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                ForwardLimit = forwardLimit
            });

            HttpContext context = CreateContext("172.68.0.1", new Dictionary<string, string>
            {
                { "X-Forwarded-For", forwardedFor }
            });

            resolver.ResolveClientIp(context).ShouldBe(expected);
        }

        /// <summary>
        /// It should ignore forwarded headers sent by peers that are not configured as proxies.
        /// </summary>
        [Fact]
        public void ItShouldIgnoreForwardedHeadersOfUntrustedPeers()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                KnownProxies = new List<string> { "172.68.0.1" }
            });

            HttpContext context = CreateContext("203.0.113.7", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            resolver.ResolveClientIp(context).ShouldBe("203.0.113.7");
        }

        /// <summary>
        /// It should accept forwarded headers of peers inside a known network.
        /// </summary>
        [Fact]
        public void ItShouldAcceptForwardedHeadersOfKnownNetworks()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                TrustLoopback = false,
                KnownNetworks = new List<string> { "172.68.0.0/16" }
            });

            HttpContext trusted = CreateContext("172.68.42.9", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            HttpContext untrusted = CreateContext("172.69.42.9", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            resolver.ResolveClientIp(trusted).ShouldBe("198.51.100.23");
            resolver.ResolveClientIp(untrusted).ShouldBe("172.69.42.9");
        }

        /// <summary>
        /// It should fall back to the peer address when the header cannot be parsed.
        /// </summary>
        [Fact]
        public void ItShouldFallBackToThePeerAddressForGarbageHeaders()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true
            });

            HttpContext context = CreateContext("172.68.0.1", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "not-an-ip" }
            });

            resolver.ResolveClientIp(context).ShouldBe("172.68.0.1");
        }

        /// <summary>
        /// It should normalize IPv4 addresses that are mapped into IPv6, so both spellings share one cache entry.
        /// </summary>
        [Fact]
        public void ItShouldNormalizeIPv4MappedAddresses()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions());
            HttpContext context = CreateContext("::ffff:198.51.100.23", null);

            resolver.ResolveClientIp(context).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should walk the chain across repeated headers, not only across comma separated values.
        /// </summary>
        [Fact]
        public void ItShouldHandleRepeatedHeaders()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                ForwardLimit = 2
            });

            DefaultHttpContext context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.68.0.1");
            context.Request.Headers["X-Forwarded-For"] = new StringValues(new[] { "198.51.100.23", "172.68.0.1" });

            resolver.ResolveClientIp(context).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should ignore empty headers and continue with the next configured one.
        /// </summary>
        [Fact]
        public void ItShouldSkipEmptyHeaders()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true
            });

            HttpContext context = CreateContext("172.68.0.1", new Dictionary<string, string>
            {
                { "CF-Connecting-IP", string.Empty },
                { "X-Forwarded-For", "198.51.100.23" }
            });

            resolver.ResolveClientIp(context).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should ignore forwarded headers from loopback when loopback is not trusted.
        /// </summary>
        [Fact]
        public void ItShouldRespectTrustLoopback()
        {
            NoBruteClientIpOptions options = new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                TrustLoopback = false,
                KnownProxies = new List<string> { "172.68.0.1" }
            };

            HttpContext context = CreateContext("127.0.0.1", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            new NoBruteClientIpResolver(null, options).ResolveClientIp(context).ShouldBe("127.0.0.1");

            options.TrustLoopback = true;

            new NoBruteClientIpResolver(null, options).ResolveClientIp(context).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should handle IPv6 proxies and bracketed IPv6 header values including ports.
        /// </summary>
        [Fact]
        public void ItShouldHandleIPv6()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                TrustLoopback = false,
                KnownNetworks = new List<string> { "2400:cb00::/32" }
            });

            HttpContext trusted = CreateContext("2400:cb00:1::5", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "[2001:db8::1]:44321" }
            });

            HttpContext untrusted = CreateContext("2a00:1450::1", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "2001:db8::1" }
            });

            resolver.ResolveClientIp(trusted).ShouldBe("2001:db8::1");
            resolver.ResolveClientIp(untrusted).ShouldBe("2a00:1450::1");
        }

        /// <summary>
        /// It should not match a network of a different address family.
        /// </summary>
        [Fact]
        public void ItShouldNotMatchNetworksOfAnotherAddressFamily()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                TrustLoopback = false,
                KnownNetworks = new List<string> { "10.0.0.0/8" }
            });

            HttpContext context = CreateContext("2001:db8::9", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            resolver.ResolveClientIp(context).ShouldBe("2001:db8::9");
        }

        /// <summary>
        /// It should ignore malformed proxy and network entries instead of throwing.
        /// </summary>
        [Fact]
        public void ItShouldIgnoreMalformedTrustEntries()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions
            {
                UseForwardedHeaders = true,
                TrustLoopback = false,
                KnownProxies = new List<string> { "not-an-ip", "172.68.0.1" },
                KnownNetworks = new List<string> { "10.0.0.0", "10.0.0.0/nope", "10.0.0.0/99", "192.168.0.0/16" }
            });

            HttpContext viaProxy = CreateContext("172.68.0.1", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            HttpContext viaNetwork = CreateContext("192.168.5.5", new Dictionary<string, string>
            {
                { "X-Forwarded-For", "198.51.100.23" }
            });

            resolver.ResolveClientIp(viaProxy).ShouldBe("198.51.100.23");
            resolver.ResolveClientIp(viaNetwork).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should build itself from the configuration when no options are passed in.
        /// </summary>
        [Fact]
        public void ItShouldConfigureItselfFromConfiguration()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "NoBrute:ClientIp:UseForwardedHeaders", "true" },
                    { "NoBrute:ClientIp:KnownProxies:0", "172.68.0.1" }
                })
                .Build());

            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(services.BuildServiceProvider());

            HttpContext context = CreateContext("172.68.0.1", new Dictionary<string, string>
            {
                { "CF-Connecting-IP", "198.51.100.23" }
            });

            resolver.ResolveClientIp(context).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should never throw when no address is available at all.
        /// </summary>
        [Fact]
        public void ItShouldReturnUnknownWithoutARemoteAddress()
        {
            NoBruteClientIpResolver resolver = new NoBruteClientIpResolver(null, new NoBruteClientIpOptions());
            DefaultHttpContext context = new DefaultHttpContext();

            resolver.ResolveClientIp(context).ShouldBe(NoBruteClientIpResolver.UnknownClientIp);
        }

        private static HttpContext CreateContext(string remoteIp, IDictionary<string, string> headers)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);

            foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
            {
                context.Request.Headers[header.Key] = header.Value;
            }

            return context;
        }
    }
}
