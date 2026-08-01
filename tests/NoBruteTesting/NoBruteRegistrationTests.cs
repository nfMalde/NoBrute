using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoBrute.Data;
using NoBrute.Domain;
using NoBrute.Models;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace NoBruteTesting
{
    /// <summary>
    /// Tests for the AddNoBrute() service registration.
    /// </summary>
    public class NoBruteRegistrationTests
    {
        /// <summary>
        /// It should register everything NoBrute needs to run.
        /// </summary>
        [Fact]
        public void ItShouldRegisterAllServices()
        {
            IServiceProvider provider = Build(services => services.AddNoBrute());

            provider.GetService<IHttpContextAccessor>().ShouldNotBeNull();
            provider.GetService<INoBruteClientIpResolver>().ShouldBeOfType<NoBruteClientIpResolver>();
            provider.GetService<INoBruteEntryLimiter>().ShouldBeOfType<NoBruteEntryLimiter>();
            provider.GetService<INoBrute>().ShouldBeOfType<NoBrute.Data.NoBrute>();
            provider.GetService<NoBrute.NoBruteAttribute>().ShouldNotBeNull();
        }

        /// <summary>
        /// It should register the Razor Pages filter only when asked for.
        /// </summary>
        /// <param name="useRazorPages">The UseRazorPages option.</param>
        /// <param name="expectRegistration">Whether the filter is expected to be resolvable.</param>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void ItShouldRegisterTheRazorPagesFilterOnDemand(bool useRazorPages, bool expectRegistration)
        {
            IServiceProvider provider = Build(services => services.AddNoBrute(options => options.UseRazorPages = useRazorPages));

            if (expectRegistration)
            {
                provider.GetService<NoBrute.NoBrutePageFilter>().ShouldNotBeNull();
            }
            else
            {
                provider.GetService<NoBrute.NoBrutePageFilter>().ShouldBeNull();
            }
        }

        /// <summary>
        /// It should not register the MVC filter when disabled.
        /// </summary>
        [Fact]
        public void ItShouldNotRegisterTheMvcFilterWhenDisabled()
        {
            IServiceProvider provider = Build(services => services.AddNoBrute(options => options.UseMvc = false));

            provider.GetService<NoBrute.NoBruteAttribute>().ShouldBeNull();
            provider.GetService<INoBrute>().ShouldNotBeNull();
        }

        /// <summary>
        /// It should apply the entry limit set in code.
        /// </summary>
        [Fact]
        public void ItShouldApplyTheEntryLimitFromRegistrationOptions()
        {
            IServiceProvider provider = Build(services => services.AddNoBrute(options => options.MaxTrackedEntries = 25));

            provider.GetService<INoBruteEntryLimiter>().MaxTrackedEntries.ShouldBe(25);
        }

        /// <summary>
        /// It should apply the client IP options set in code.
        /// </summary>
        [Fact]
        public void ItShouldApplyTheClientIpOptionsFromRegistrationOptions()
        {
            IServiceProvider provider = Build(services => services.AddNoBrute(options =>
                options.ClientIp = new NoBruteClientIpOptions
                {
                    UseForwardedHeaders = true,
                    KnownProxies = new List<string> { "172.68.0.1" }
                }));

            DefaultHttpContext context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.68.0.1");
            context.Request.Headers["CF-Connecting-IP"] = "198.51.100.23";

            provider.GetService<INoBruteClientIpResolver>().ResolveClientIp(context).ShouldBe("198.51.100.23");
        }

        /// <summary>
        /// It should apply the blocked status code set in code.
        /// </summary>
        [Fact]
        public void ItShouldApplyTheBlockedStatusCodeFromRegistrationOptions()
        {
            IServiceProvider provider = Build(services => services.AddNoBrute(options =>
            {
                options.MaxTrackedEntries = 1;
                options.BlockedStatusCode = 503;
            }));

            IHttpContextAccessor accessor = provider.GetService<IHttpContextAccessor>();
            accessor.HttpContext = CreateRequest("203.0.113.7");

            INoBrute noBrute = provider.GetService<INoBrute>();
            noBrute.CheckRequest("FLOODED_REQUEST").IsBlocked.ShouldBeFalse();

            accessor.HttpContext = CreateRequest("203.0.113.8");

            NoBruteRequestCheck check = noBrute.CheckRequest("FLOODED_REQUEST");

            check.IsBlocked.ShouldBeTrue();
            check.BlockedStatusCode.ShouldBe(503);
        }

        /// <summary>
        /// It should keep a custom resolver registered before AddNoBrute().
        /// </summary>
        [Fact]
        public void ItShouldKeepACustomClientIpResolver()
        {
            IServiceProvider provider = Build(services =>
            {
                services.AddSingleton<INoBruteClientIpResolver, FixedClientIpResolver>();
                services.AddNoBrute();
            });

            provider.GetService<INoBruteClientIpResolver>().ShouldBeOfType<FixedClientIpResolver>();
        }

        /// <summary>
        /// It should keep a custom entry limiter registered before AddNoBrute().
        /// </summary>
        [Fact]
        public void ItShouldKeepACustomEntryLimiter()
        {
            IServiceProvider provider = Build(services =>
            {
                services.AddSingleton<INoBruteEntryLimiter>(new NoBruteEntryLimiter(7));
                services.AddNoBrute(options => options.MaxTrackedEntries = 999);
            });

            provider.GetService<INoBruteEntryLimiter>().MaxTrackedEntries.ShouldBe(7);
        }

        /// <summary>
        /// It should reject a null configuration action.
        /// </summary>
        [Fact]
        public void ItShouldThrowWithoutAConfigureAction()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddNoBrute(null));
        }

        private static DefaultHttpContext CreateRequest(string remoteIp)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/";
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);

            return context;
        }

        private static IServiceProvider Build(Action<IServiceCollection> configure)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddLogging();
            services.AddMemoryCache();

            configure(services);

            return services.BuildServiceProvider();
        }

        private sealed class FixedClientIpResolver : INoBruteClientIpResolver
        {
            public string ResolveClientIp(HttpContext context) => "fixed";
        }
    }
}
