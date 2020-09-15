using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

public static class DistributedCachedLightWebRequest
{
    public static Task<LightHttpRequest.RequestResult<T>> SendAsync<T>(
        Func<HttpResponseMessage, Task<T>> responseHandler,
        IDistributedCache cache,
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        bool onlyParseBodyOnSuccess = true,
        CancellationToken cancellationToken = default(CancellationToken),
        DistributedCacheEntryOptions cacheExpiration = null)
    {
        return DistributedCachedLightWebRequest.SendAsyncInternal(responseHandler, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
    }

    // convenience methods
    public static Task<LightHttpRequest.RequestResult<T>> SendAsync<T>(
        IDistributedCache cache,
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        bool onlyParseBodyOnSuccess = true,
        CancellationToken cancellationToken = default(CancellationToken),
        DistributedCacheEntryOptions cacheExpiration = null)
    {
        return DistributedCachedLightWebRequest.SendAsyncInternal<T>(null, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
    }

    // private
    private static async Task<LightHttpRequest.RequestResult<T>> SendAsyncInternal<T>(
        Func<HttpResponseMessage, Task<T>> responseHandler,
        IDistributedCache cache,
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        bool onlyParseBodyOnSuccess = true,
        CancellationToken cancellationToken = default(CancellationToken),
        DistributedCacheEntryOptions cacheExpiration = null)
    {
        var fullUri = LightHttpRequest.GetFullUri(client, uri);

        var cachedString = await cache.GetStringAsync(fullUri.ToString(), cancellationToken);
        if (!string.IsNullOrEmpty(cachedString))
        {
            return new LightHttpRequest.RequestResult<T>()
            {
                Value = JsonConvert.DeserializeObject<T>(cachedString),
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
            await cache.SetStringAsync(fullUri.ToString(), JsonConvert.SerializeObject(requestResult.Value), cacheExpiration, cancellationToken);

        return requestResult;
    }
}
