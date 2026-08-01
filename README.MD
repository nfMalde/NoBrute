[![Nuget](https://img.shields.io/nuget/v/NoBrute?style=flat-square)](https://www.nuget.org/packages/NoBrute/) [![Downloads](https://img.shields.io/nuget/dt/NoBrute)](https://www.nuget.org/packages/NoBrute/) [![Paypal Donate](https://img.shields.io/badge/donate-paypal-blue)](https://www.paypal.com/donate/?hosted_button_id=SVZHLRTQ6H4VL) [![Pull Request Check](https://img.shields.io/github/actions/workflow/status/nfMalde/NoBrute/pr.yml)](https://github.com/nfMalde/NoBrute/actions/workflows/pr.yml)

# NoBrute (by Malte)  
Simple and lightweight brute-force protection for .NET 10.  
This library will protect defined actions in your MVC controllers, Minimal API endpoints, and Razor Pages by making them inefficient to brute-force.  
It will append request times in milliseconds if a local cache entry on the server is found for the same request & request name & method, and the hit count reaches a defined limit (referred to here as "green requests") within a specific time frame.

## Requirements  
NoBrute requires at least one `IMemoryCache` or `IDistributedCache` to be registered in your application. (For obvious reasons, storing the information in the session won't work because bots will never send cookies along with their requests.)

**External Libraries**  
This library uses the following library to achieve its functionality:  
* [Shouldly](https://github.com/shouldly)

## Install  
Using the NuGet package manager:  
```bash
Install-Package NoBrute
```

Using the .NET CLI:  
```bash
dotnet add package NoBrute
```

Enable it in your application:  
```csharp
// Startup.cs / Program.cs

public IServiceProvider ConfigureServices(IServiceCollection services) {
     
    // Use Memory Cache:
    services.AddMemoryCache();
    // Or a distributed cache (NoBrute will prefer this if both are registered)
    services.AddStackExchangeRedisCache(x =>
    {
        x.Configuration = "... ";
    }); // In this case, we used Redis as an example

    // MVC only (default):
    services.AddNoBrute();

    // Or configure which filters to register:
    services.AddNoBrute(options =>
    {
        options.UseMvc = true;              // Register MVC action filter (default: true)
        options.UseRazorPages = true;       // Register Razor Pages filter (default: false)
        options.MaxTrackedEntries = 50000;  // Circuit breaker (default: from configuration, 0 = unlimited)
        options.ClientIp = new NoBruteClientIpOptions { UseForwardedHeaders = true };
    });
}
```

## Configuration  
No configuration is required to use NoBrute. Here is a JSON example for your `appsettings.json` to configure NoBrute and the default values used if the entry does not exist in your configuration:

```json
{
  "NoBrute": {
    "Enabled": true,
    "GreenRetries": 10,
    "IncreaseRequestTime": 20,
    "MaxIncreaseRequestTime": 0,
    "TimeUntilReset": 2,
    "TimeUntilResetUnit": "H",
    "MaxTrackedEntries": 0,
    "BlockedStatusCode": 429,
    "StatusCodesForAutoProcess": [
      200
    ],
    "ClientIp": {
      "UseForwardedHeaders": false,
      "Headers": [ "CF-Connecting-IP", "X-Forwarded-For" ],
      "ForwardLimit": 1,
      "KnownProxies": [],
      "KnownNetworks": [],
      "TrustLoopback": true
    }
  }
}
```

### Configuration Entries and Their Meanings  

| Configuration Entry Name | Description                                                                 | Default Value | Type       |
|---------------------------|-----------------------------------------------------------------------------|---------------|------------|
| Enabled                   | If true, the NoBrute service is enabled                                    | true          | Boolean    |
| GreenRetries              | If this count of the same requests is reached, NoBrute will start appending request time (asynchronously, without blocking a thread) | 10            | Integer    |
| IncreaseRequestTime       | For each request that exceeds the `GreenRetries` entry number, NoBrute will append n ms to the request | 20            | Integer    |
| MaxIncreaseRequestTime    | Upper bound in ms for the delay added to a single request. `0` means unlimited. Useful to stop an attacker from keeping thousands of connections open for minutes | 0             | Integer    |
| TimeUntilReset            | This, in combination with `TimeUntilResetUnit`, declares the time when the saved request count for a user will be cleared so the user gets normal request times again. It is also used as the absolute expiration of the cache entry | 2             | Integer    |
| TimeUntilResetUnit        | This is the unit of time used for the value of `TimeUntilReset`. Possible values: `Years = 'y', Days = 'd', Months = 'M', Hours = 'H', Minutes = 'i', Seconds = 's', Milliseconds = 'n'` | H (Hours)     | String     |
| MaxTrackedEntries         | Maximum number of clients tracked at the same time (circuit breaker, see below). `0` means unlimited | 0             | Integer    |
| BlockedStatusCode         | Status code returned to new clients while `MaxTrackedEntries` is reached | 429           | Integer    |
| StatusCodesForAutoProcess | This is for auto-processing requests. (More details in the "Usage" section below.) You can declare here which status codes of an `IHttpAction` will remove saved requests automatically | [200]         | Integer[]  |
| ClientIp                  | Client IP resolution behind reverse proxies, see below                     | see below     | Object     |

## Running Behind Cloudflare, a Load Balancer or a Reverse Proxy  

NoBrute stores one cache entry per client IP. Behind a proxy, `HttpContext.Connection.RemoteIpAddress` is the
proxy, so without further configuration **every** visitor shares a single entry and honest customers would be
slowed down after a few requests worldwide.

Enable forwarded headers to make NoBrute use the real client address:

```json
{
  "NoBrute": {
    "ClientIp": {
      "UseForwardedHeaders": true,
      "Headers": [ "CF-Connecting-IP", "X-Forwarded-For" ],
      "ForwardLimit": 1,
      "KnownNetworks": [ "173.245.48.0/20", "103.21.244.0/22" ]
    }
  }
}
```

| Configuration Entry Name | Description                                                                 | Default Value | Type       |
|---------------------------|-----------------------------------------------------------------------------|---------------|------------|
| UseForwardedHeaders       | If true, the headers below are used to determine the client IP             | false         | Boolean    |
| Headers                   | Headers that are inspected, in order. The first header containing a parsable IP wins | ["CF-Connecting-IP", "X-Forwarded-For"] | String[] |
| ForwardLimit              | Number of proxies between client and server. For a chain (`client, proxy1`) the entry `ForwardLimit` positions from the right is used | 1             | Integer    |
| KnownProxies              | IP addresses whose forwarded headers are trusted                            | []            | String[]   |
| KnownNetworks             | CIDR networks whose forwarded headers are trusted                           | []            | String[]   |
| TrustLoopback             | Always trust forwarded headers coming from loopback addresses               | true          | Boolean    |

> **Security note:** forwarded headers are attacker controlled. Always list your proxy in `KnownProxies` or
> `KnownNetworks` (for Cloudflare: their published IP ranges). If both lists are empty, NoBrute accepts the
> headers from every peer and logs a warning during startup — anyone able to reach the application directly
> could then send a random client IP per request and bypass the protection completely.

You can also configure this in code, or replace the resolution entirely by registering your own
`INoBruteClientIpResolver` before calling `AddNoBrute()`:

```csharp
services.AddNoBrute(options =>
{
    options.ClientIp = new NoBruteClientIpOptions
    {
        UseForwardedHeaders = true,
        KnownNetworks = { "173.245.48.0/20" }
    };
});
```

## Circuit Breaker: Limiting Tracked Clients  

A botnet using millions of distinct source addresses would otherwise flood the memory cache or Redis with
temporary entries. Set `MaxTrackedEntries` to cap how many clients are tracked at the same time per
application instance:

```json
{
  "NoBrute": {
    "MaxTrackedEntries": 50000,
    "BlockedStatusCode": 429
  }
}
```

Once the limit is reached, **already known** clients keep being processed normally, while requests from
new clients are answered immediately with `BlockedStatusCode` (default `429 Too Many Requests`) without
allocating another cache entry. Entries free their slot when they expire (`TimeUntilReset`) or when they
are released via auto-processing.

All cache entries are written with an absolute expiration of `TimeUntilReset`, so nothing lingers in
memory or Redis forever.

The check result exposes this state as `NoBruteRequestCheck.IsBlocked` / `BlockedStatusCode`, and all three
filters short-circuit the request when it is set. If you need a different strategy, register your own
`INoBruteEntryLimiter` before calling `AddNoBrute()`.

> **Note:** the limit is counted per application instance. In a web farm sharing one Redis, size it per
> instance (e.g. four instances × 50.000 = up to 200.000 entries in Redis). Because the counter is local,
> a client that is known to Redis but not to a restarted instance is treated as a new client — while the
> limit is reached it would be blocked there until it is tracked again.

## Usage  

### The Action Filter Attribute (Web API or MVC)  
To protect an action, you can use the `NoBruteAttribute`.  
This is the simple way.

#### Arguments:  
| Name                  | Description                                                                 |
|-----------------------|-----------------------------------------------------------------------------|
| string requestName    | Assigns a fixed name to the incoming request for better identification. If null, empty, or not given, NoBrute will use the `RequestPath` as the name. |
| bool autoProcess      | Indicates that the requests should be released/cleared when the configured (see above) HTTP status code is returned by the action. (Default: false) |

#### Examples  
**Generated Name**  
```csharp
[NoBrute]
public IHttpActionResult Login() {
    ...
}
```

**Generated Name with Auto Release**  
```csharp
[NoBrute(true)]
public IHttpActionResult Login() {
    ...
}
```

**Fixed Name**  
```csharp
[NoBrute("MyFixedName")]
public IHttpActionResult Login() {
    ...
}
```

**Fixed Name with Auto Release**  
```csharp
[NoBrute("MyFixedName", true)]
public IHttpActionResult Login() {
    ...
}
```

### The Endpoint Filter (Minimal API)  
To protect a Minimal API endpoint, use the `WithNoBrute()` extension method on the route handler builder.

#### Arguments:  
| Name                  | Description                                                                 |
|-----------------------|-----------------------------------------------------------------------------|
| string requestName    | Assigns a fixed name to the incoming request for better identification. If null, empty, or not given, NoBrute will use the `RequestPath` as the name. |
| bool autoProcess      | Indicates that the requests should be released/cleared when the configured HTTP status code is returned. (Default: true) |

#### Examples  
**Generated Name with Auto Release**  
```csharp
app.MapPost("/login", (LoginRequest req) => {
    ...
}).WithNoBrute();
```

**Fixed Name**  
```csharp
app.MapPost("/login", (LoginRequest req) => {
    ...
}).WithNoBrute("LoginEndpoint");
```

**Fixed Name with Auto Release disabled**  
```csharp
app.MapPost("/login", (LoginRequest req) => {
    ...
}).WithNoBrute("LoginEndpoint", false);
```

### The Page Filter (Razor Pages)  
To protect a Razor Page, use the `NoBrutePageFilter`. You can register it globally or apply it to individual pages.

#### Arguments:  
| Name                  | Description                                                                 |
|-----------------------|-----------------------------------------------------------------------------|
| string requestName    | Assigns a fixed name to the incoming request for better identification. If null, empty, or not given, NoBrute will use the `RequestPath` as the name. |
| bool autoProcess      | Indicates that the requests should be released/cleared when the configured HTTP status code is returned. (Default: true) |

#### Examples  
**Register globally for all pages**  
```csharp
builder.Services.AddRazorPages(options =>
{
    options.Filters.Add(new NoBrutePageFilter("GlobalPages", true));
});
```

### The Service  
If you have a more complex design to decide when a request should be checked or not, you can also use the service.

**Inject Service**  
```csharp
private readonly INoBrute nobrute;

public MyController(INoBrute nobrute) {
    this.nobrute = nobrute;
}
```

**Use it in the Method:**  
```csharp
public async Task<IActionResult> MyAction() {
    if (1 > 0)  // or some if-else logic
    {
        NoBruteRequestCheck check = await this.nobrute.CheckRequestAsync("MyActionRequestName");

        if (check.IsBlocked)
        {
            return StatusCode(check.BlockedStatusCode);
        }

        if (!check.IsGreenRequest)
        {
            await Task.Delay(check.AppendRequestTime, HttpContext.RequestAborted);
        }

        // Some more logic
    }
}
```

The `CheckRequestAsync` method will return an object of type `NoBruteRequestCheck`.  
It will contain the flag `IsGreenRequest` and how much time to append to the request, the `IsBlocked` flag
for the circuit breaker, and some user information like the resolved client IP.

The service checks and releases requests for you but never delays them — that is up to you here.
**Always use `await Task.Delay(...)`, never `Thread.Sleep(...)`:** `Thread.Sleep` blocks an operating system
thread, so a large bot wave would exhaust the Kestrel thread pool (thread starvation) and take the whole
application down. `Task.Delay` releases the thread for other customers while the attacker waits.

All three filters (`NoBruteAttribute`, `NoBruteEndpointFilter`, `NoBrutePageFilter`) already work this way —
they only implement the asynchronous filter interfaces, which ASP.NET Core prefers anyway, and they use the
asynchronous cache API (`GetAsync`/`SetAsync`) so a slow Redis never blocks a thread either.

Synchronous `CheckRequest`, `ReleaseRequest` and `AutoProcessRequestRelease` still exist for compatibility.
Avoid them in request pipelines: with an `IDistributedCache` they block the calling thread while waiting for
the cache.

See more at `/src/Domain/INoBrute.cs` and `/src/Models/NoBruteRequestCheck.cs` in the GitHub repository.

## Contribute / Donations  
If you have any ideas to improve my projects, feel free to send a pull request.  

If you like my work and want to support me (or want to buy me a coffee/beer), PayPal donations are more than appreciated.

[![Paypal Donate](https://img.shields.io/badge/donate-paypal-blue)](https://www.paypal.com/donate/?hosted_button_id=SVZHLRTQ6H4VL)![Nuget](https://img.shields.io/nuget/v/NoBrute?style=flat-square)](https://www.nuget.org/packages/NoBrute/) 