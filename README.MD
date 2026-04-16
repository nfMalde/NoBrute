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
// Startup.cs

public IServiceProvider ConfigureServices(IServiceCollection services) {
     
    // Use Memory Cache:
    services.AddMemoryCache();
    // Or a distributed cache (NoBrute will prefer this if both are registered)
    services.AddStackExchangeRedisCache(x =>
    {
        x.Configuration = "... ";
    }); // In this case, we used Redis as an example

    services.AddNoBrute();
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
    "TimeUntilReset": 2,
    "TimeUntilResetUnit": "H",
    "StatusCodesForAutoProcess": [
      200
    ]
  }
}
```

### Configuration Entries and Their Meanings  

| Configuration Entry Name | Description                                                                 | Default Value | Type       |
|---------------------------|-----------------------------------------------------------------------------|---------------|------------|
| Enabled                   | If true, the NoBrute service is enabled                                    | true          | Boolean    |
| GreenRetries              | If this count of the same requests is reached, NoBrute will start appending request time by setting the thread to sleep for n ms | 10            | Integer    |
| IncreaseRequestTime       | For each request that exceeds the `GreenRetries` entry number, NoBrute will append n ms to the request | 20            | Integer    |
| TimeUntilReset            | This, in combination with `TimeUntilResetUnit`, declares the time when the saved request count for a user will be cleared so the user gets normal request times again | 2             | Integer    |
| TimeUntilResetUnit        | This is the unit of time used for the value of `TimeUntilReset`. Possible values: `Years = 'y', Days = 'd', Months = 'M', Hours = 'H', Minutes = 'i', Seconds = 's', Milliseconds = 'n'` | H (Hours)     | String     |
| StatusCodesForAutoProcess | This is for auto-processing requests. (More details in the "Usage" section below.) You can declare here which status codes of an `IHttpAction` will remove saved requests automatically | [200]         | Integer[]  |

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
public IHttpActionResult MyAction() {
    if (1 > 0)  // or some if-else logic
    {
        NoBruteRequestCheck check = this.nobrute.CheckRequest("MyActionRequestName");

        // Some more logic
    }
}
```

The `CheckRequest` method will return an object of type `NoBruteRequestCheck`.  
It will contain the flag `IsGreenRequest` and how much time to append to the request.  
Additionally, some user information like IP will be returned.

However, you have to call `Thread.Sleep` yourself here. The service will only release and check requests for you but never sleep the requests like the action attribute.

See more at `/src/Domain/INoBrute.cs` and `/src/Models/NoBruteRequestCheck.cs` in the GitHub repository.

## Contribute / Donations  
If you have any ideas to improve my projects, feel free to send a pull request.  

If you like my work and want to support me (or want to buy me a coffee/beer), PayPal donations are more than appreciated.

[![Paypal Donate](https://img.shields.io/badge/donate-paypal-blue)](https://www.paypal.com/donate/?hosted_button_id=SVZHLRTQ6H4VL)![Nuget](https://img.shields.io/nuget/v/NoBrute?style=flat-square)](https://www.nuget.org/packages/NoBrute/) 