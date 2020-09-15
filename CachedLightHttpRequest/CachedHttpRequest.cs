using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;

namespace LightHttpRequest.Caching
{
    public static class CachedHttpRequest
    {
        public static Task<HttpRequestResult<T>> SendAsync<T>(
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
            return CachedHttpRequest.SendAsyncInternal(responseHandler, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
        }

        // convenience methods
        public static Task<HttpRequestResult<T>> SendAsync<T>(
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
            return CachedHttpRequest.SendAsyncInternal<T>(null, cache, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken, cacheExpiration);
        }

        // private
        private static async Task<HttpRequestResult<T>> SendAsyncInternal<T>(
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
            var fullUri = HttpRequest.GetFullUri(client, uri);

            T cachedValue;
            if (cache.TryGetValue(fullUri, out cachedValue))
            {
                return new HttpRequestResult<T>()
                {
                    Value = cachedValue,
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
                cache.Set(fullUri, requestResult.Value, cacheExpiration);

            return requestResult;
        }
    }
}
