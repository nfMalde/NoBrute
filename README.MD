[![Nuget](https://img.shields.io/nuget/v/NoBrute?style=flat-square)](https://www.nuget.org/packages/NoBrute/) 
 [![Downloads](https://img.shields.io/nuget/dt/NoBrute?style=flat-square)](https://www.nuget.org/packages/NoBrute/)
 [![Paypal Donate](https://www.paypalobjects.com/en_US/i/btn/btn_donate_SM.gif)](https://www.paypal.com/donate/?hosted_button_id=SVZHLRTQ6H4VL)

# NoBrute (by Malte)
Simple and light bruteforce protection for .NET 8
This Lib will protect defined actions in your controllers in making them inefficient to be bruteforced for simple soulutions.
It will append request times in ms if a local cache entry on the server was found for the same request & request name & method and the hit count reaches an defined limit (so called here: greenrequests) in an amount of time.

## Requirements
NoBrute will require at least one IMemoryCache or IDistributedCache to be regisrted in your application. (Since for obvious reasons storing the info the session wont work cause bots will never send coookies along them)

**External Libraries**
This Library uses the following lib(s) fro archive its functionality:
* [Shouldly](https://github.com/shouldly)

## Install
Using the nuget package manager:
```
Install-Package NoBrute
```

Using the dotnet cli:
```
dotnet add package NoBrute
```

Enable it in your application:
```C#
// Startup.cs

 public IServiceProvider ConfigureServices(IServiceCollection services) {
     
     // Use Memory Cache:
    services.AddMemoryCache();
    // Or an distributed cache (NoBrute will prefer this if both registered)
     services.AddStackExchangeRedisCache(x =>
            {
                x.Configuration = "... ";
            }); // In this case we used for example redis

    services.AddNoBrute();
 }
```


## Configuration
In order to use NoBrute there is no configuration required. Here is a JSON Example for your "appsettings.json" to configure NoBrute and what default values are used if the entry does not exists in you configuration:

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
### Configuration Entries and their meanings

Configuration Entry Name | Description | Default Value | Type
------------ | ------------- | ------------- | -------------
Enabled | If true the NoBrute Service is enabled | true | Boolean
GreenRetries | If this count of same requests is reached NoBrute will start appending request time by setting the thread to sleep for n ms | 10 | Integer
IncreaseRequestTime | For each request that exceeds the  GreenRetries entry number NoBrute will append n ms to the request | 20 | Integer
TimeUntilReset | This in combination with TimeUntilResetUnit will declare the time when the saved request count for a user will be cleared so the user gets normal request times again | 2 | Integer
TimeUntilResetUnit | This is the unit of time used for the value of TimeUntilReset. Possible values: `Years = 'y', Days = 'd', Months = 'M', Hours = 'H', Minutes = 'i', Seconds = 's', Miliseconds = 'n'` | H (Hours) | String
StatusCodesForAutoProcess | This is for autoprocessing requests.  (More details see below "Usage"). You can declare here what status codes of an IHttpAction will removed saved request automatically | [ 200 ] | Integer[]


## Usage

### The Action Filter Attribute (WebApi or MVC)
To protect an action you can use the "NoBruteAttribute".
This is the simnple way.

#### Arguments:
Name | Description 
------------ | ------------
string requestName | Gives an fixed name to the incoming request to better identify it. If null, empty nur not given NoBrute will use the RequestPath as name
bool autoProcess  | Indicated that the requests should be released / cleared when the configurated (See above) HTPPStatusCode is returned by the action. (Default: false)

#### Examples
**Generated Name**
```C#
[NoBrute]
public IHttpActionResult Login() {
    ...
}
```

***Generated Name with auto release**
```C#
[NoBrute(true)]
public IHttpActionResult Login() {
    ...
}
```

**Fixed Name**
```C#
[NoBrute("MyFixedName")]
public IHttpActionResult Login() {
    ...
}
```

**Fixed Name  with auto release**
```C#
[NoBrute("MyFixedName", true)]
public IHttpActionResult Login() {
    ...
}
```


### The Service
If you have a more complex design to decide when a request should be checked or not you can also use the Service.

**Inject Service**
```C#
private readonly INoBrute nobrute;

public MyController(INoBrute nobrute) {
    this.nobrute = nobrute;
}
```

**Use it in the method:**

```C#
public IHttpActionResult MyAction() {
    if (1 > 0)  // or some if else logic
    {
        NoBruteRequestCheck check = this.nobrute.CheckRequest("MyActionRequestName");

        // Some more logic
    }
}

```

The "CheckRequest" Method will return an Object of type NoBruteRequestCheck.
It will contain the flag "IsGreenRequest" and how much time to append to the request.
Also some user infos like IP will be returned.

However you have to call the Thread. Sleep by yourself here. The Service will only release and check requests for you but never sleep the requests like the action attribute.

See more at `/src/Domain/INoBrute.cs` and `/src/Models/NoBruteRequestCheck.cs` in the Github Repository.


## Contribute / Donations
If you got any Ideas to improve my projects feel free to send an pull request. 

If you like my work and want to support me (or want to buy me a coffee/beer) paypal donation are more than appreciated.

 [![Paypal Donate](https://www.paypalobjects.com/en_US/DK/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=SVZHLRTQ6H4VL)



## Changelog

Version | Changes 
------------ | ------------
2.0.0 | Upgraded to .net 8
1.2.1 | Fixed Bug with non-binary formatter and fixed cache bug
1.2.0 | Migrated to .NET 6, Removed binary formatter
1.1.0 | Migrated to .NET 5
1.0.1 | Updated Dependencies and added automated readme.md for nuget
1.0.0 | Initial Release
