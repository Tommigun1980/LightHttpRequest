using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;

public static class CachedLightHttpRequest
{
    public static Task<LightHttpRequest.RequestResult<T>> SendAsync<T>(
        Func<HttpResponseMessage, Task<T>> responseHandler,
        IMemoryCache cache,
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        bool onlyParseBodyOnSuccess = true,
        CancellationToken cancellationToken = default(CancellationToken),
        MemoryCacheEntryOptions cacheExpiration = null)
    {
        return CachedLightHttpRequest.SendAsyncInternal(responseHandler, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
    }

    // convenience methods
    public static Task<LightHttpRequest.RequestResult<T>> SendAsync<T>(
        IMemoryCache cache,
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        bool onlyParseBodyOnSuccess = true,
        CancellationToken cancellationToken = default(CancellationToken),
        MemoryCacheEntryOptions cacheExpiration = null)
    {
        return CachedLightHttpRequest.SendAsyncInternal<T>(null, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
    }

    // private
    private static async Task<LightHttpRequest.RequestResult<T>> SendAsyncInternal<T>(
        Func<HttpResponseMessage, Task<T>> responseHandler,
        IMemoryCache cache,
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        bool onlyParseBodyOnSuccess = true,
        CancellationToken cancellationToken = default(CancellationToken),
        MemoryCacheEntryOptions cacheExpiration = null)
    {
        var fullUri = LightHttpRequest.GetFullUri(client, uri);

        T cachedValue;
        if (cache.TryGetValue(fullUri, out cachedValue))
        {
            return new LightHttpRequest.RequestResult<T>()
            {
                Value = cachedValue,
                Status = new LightHttpRequest.RequestStatus()
                {
                    Success = true
                }
            };
        }

        var requestResult = await (responseHandler != null
            ? LightHttpRequest.SendAsync(responseHandler, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken)
            : LightHttpRequest.SendAsync<T>(client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken));

        if (requestResult.Status.Success)
            cache.Set(fullUri, requestResult.Value, cacheExpiration);

        return requestResult;
    }
}
