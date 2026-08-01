# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
## [2.3.0]
### Added
- Asynchronous service API: `CheckRequestAsync`, `ReleaseRequestAsync` and `AutoProcessRequestReleaseAsync`.
  They ship with default interface implementations delegating to the synchronous overloads, so existing custom
  `INoBrute` implementations keep compiling and working
- Client IP resolution behind reverse proxies (Cloudflare, load balancers) via the `NoBrute:ClientIp` configuration section,
  supporting `CF-Connecting-IP` / `X-Forwarded-For`, a forward limit and trusted proxies/networks (`INoBruteClientIpResolver`)
- Global circuit breaker `NoBrute:MaxTrackedEntries`: once the configured number of tracked clients is reached, unknown clients
  are answered with `NoBrute:BlockedStatusCode` (default `429`) instead of allocating further cache entries (`INoBruteEntryLimiter`)
- `NoBrute:MaxIncreaseRequestTime` to cap the delay added to a single request
- `NoBruteRequestCheck.IsBlocked` and `NoBruteRequestCheck.BlockedStatusCode`
- `NoBruteRegistrationOptions` can now configure client IP handling, the entry limit, the blocked status code and the delay cap in code

### Changed
- `NoBruteAttribute` no longer overrides `OnActionExecuting` / `OnActionExecuted`, and `NoBrutePageFilter.OnPageHandlerExecuting` /
  `OnPageHandlerExecuted` are now empty. All logic lives in the asynchronous filter methods, which ASP.NET Core prefers anyway.
  This only affects code that invoked those methods directly (e.g. tests); the request pipeline behaves as before
- Removed the last blocking calls from the request pipeline: no more `Thread.Sleep` and no more `GetAwaiter().GetResult()`.
  Delays use `await Task.Delay(...)` and the distributed cache is accessed via `GetAsync`/`SetAsync`/`RemoveAsync`,
  so a bot wave can no longer starve the Kestrel thread pool
- Cache entries are now written with an absolute expiration of `TimeUntilReset` instead of living forever
- Releasing the last request of a client removes its cache entry and frees its circuit breaker slot
- `AddNoBrute()` registers `IHttpContextAccessor` and the new services (both replaceable via `TryAdd` semantics)
- Missing or malformed configuration values now fall back to their defaults instead of throwing

### Fixed
- `TimeUntilResetUnit` was parsed by enum member name and therefore never matched, so every configured unit
  silently fell back to hours (`H` only worked by accident). The documented characters `y`, `d`, `M`, `H`, `i`,
  `s` and `n` now work as described — check your configuration if you relied on the previous behaviour

## [2.2.0]
### Added
- Added Minimal API support via `NoBruteEndpointFilter` and `WithNoBrute()` endpoint extension
- Added Razor Pages support via `NoBrutePageFilter`

### Changed
- Updated to .NET 10.0
- Replaced individual NuGet package references with `FrameworkReference` to `Microsoft.AspNetCore.App`
- Removed unnecessary dependencies (`System.Text.RegularExpressions`, legacy ASP.NET Core 2.x packages)

## [2.1.0]
## Changed
- Updated to .NET 9.0
- Cleaned up code and removed unnecessary dependencies
- Added more detailed documentation and examples
- Improved nullability annotations
- Changed packages to be compatible with .NET 9.0

## [2.0.0]
### Added
- Upgraded to .NET 8.

## [1.2.1] 
### Fixed
- Resolved bug with non-binary formatter.
- Fixed cache-related issue.

## [1.2.0]
### Changed
- Migrated to .NET 6.
- Removed binary formatter.

## [1.1.0]
### Changed
- Migrated to .NET 5.

## [1.0.1] 
### Added
- Updated dependencies.
- Added automated README.md generation for NuGet.

## [1.0.0]
### Added
- Initial release.