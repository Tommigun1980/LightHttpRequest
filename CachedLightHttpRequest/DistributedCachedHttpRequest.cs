using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace LightHttpRequest.Caching.Distributed
{
    public static class CachedHttpRequest
    {
        public static Task<HttpRequestResult<T>> SendAsync<T>(
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
            return CachedHttpRequest.SendAsyncInternal(responseHandler, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
        }

        // convenience methods
        public static Task<HttpRequestResult<T>> SendAsync<T>(
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
            return CachedHttpRequest.SendAsyncInternal<T>(null, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
        }

        // private
        private static async Task<HttpRequestResult<T>> SendAsyncInternal<T>(
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
            var fullUri = HttpRequest.GetFullUri(client, uri);

            var cachedString = await cache.GetStringAsync(fullUri.ToString(), cancellationToken);
            if (!string.IsNullOrEmpty(cachedString))
            {
                return new HttpRequestResult<T>()
                {
                    Value = JsonConvert.DeserializeObject<T>(cachedString),
                    Status = new HttpRequestStatus()
                    {
                        Success = true
                    }
                };
            }

            var requestResult = await (responseHandler != null
                ? HttpRequest.SendAsync(responseHandler, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken)
                : HttpRequest.SendAsync<T>(client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken));

            if (requestResult.Status.Success)
                await cache.SetStringAsync(fullUri.ToString(), JsonConvert.SerializeObject(requestResult.Value), cacheExpiration, cancellationToken);

            return requestResult;
        }
    }

}
