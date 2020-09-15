# LightHttpRequest
*A wrapper for HttpClient that simplifies things, and uses normal error codes instead of exceptions for network errors*

NuGet package available at https://www.nuget.org/packages/LightHttpRequest/

Caching version (local cache or distributed cache) package available at https://www.nuget.org/packages/CachedLightHttpRequest/

## Intro

.NET's [HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=netcore-3.1) can be cumbersome to work with for the following reasons:
* It uses exceptions to signal of connection and Http errors
* It is easy to accidentally write code that leaks resources
* A lot of boiler-plate code is required to accomplish common things

LightHttpRequest is a wrapper around HttpClient that solves all of the above issues.

A caching version, utilizing either [IMemoryCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-3.1) or [IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-3.1), is also available in a separate package.

## Usage

Documentation forthcoming.
