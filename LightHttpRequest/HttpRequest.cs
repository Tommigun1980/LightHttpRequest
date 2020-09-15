using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;

namespace LightHttpRequest
{
    public class HttpRequestStatus
    {
        // true: Request was succesful.
        // false: Request failed, and either RequestException or StatusCode contains the error.
        public bool Success { get; set; }

        // If failure was due to a network exception exception, it's here.
        // (Only HttpRequestException, TimeoutException and OperationCanceledException are caught).
        public Exception RequestException { get; set; }

        // If failure was due to the status code being invalid, it's here.
        public HttpStatusCode StatusCode { get; set; }

        // A description of the error. If the server outputted some additional information, it's here.
        public string ReasonPhrase { get; set; }

        public override string ToString()
        {
            return this.Success ? "Success" : this.ReasonPhrase;
        }
    }

    public class HttpRequestResult<T>
    {
        public HttpRequestStatus Status { get; set; }

        public T Value { get; set; }
    }

    public static class HttpRequest
    {
        public static async Task<HttpRequestStatus> SendAsync(
            HttpClient client,
            HttpMethod method,
            string uri = null,
            HttpContent requestContent = null,
            IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await HttpRequest.SendAsyncInternal(client, method, uri, requestContent, headers, cancellationToken);
            using (result.responseMessage)
            {
                return result.status;
            }
        }

        public static async Task<HttpRequestResult<T>> SendAsync<T>(
            Func<HttpResponseMessage, Task<T>> responseHandler,
            HttpClient client,
            HttpMethod method,
            string uri = null,
            HttpContent requestContent = null,
            IDictionary<string, string> headers = null,
            bool onlyParseBodyOnSuccess = true,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await HttpRequest.SendAsyncInternal(client, method, uri, requestContent, headers, cancellationToken);
            using (result.responseMessage)
            {
                if (!result.status.Success && onlyParseBodyOnSuccess)
                    return new HttpRequestResult<T>() { Status = result.status };

                try
                {
                    var convertedValue = await responseHandler(result.responseMessage);
                    return new HttpRequestResult<T>() { Value = convertedValue, Status = result.status };
                }
                catch (Exception e)
                {
                    var fullUri = GetFullUri(client, uri);
                    Console.WriteLine($"Error - Object conversion failed for {fullUri}: {e}");
                    ThrowIfNonHttpException(e, fullUri.ToString());

                    return new HttpRequestResult<T>()
                    {
                        Status = new HttpRequestStatus() { RequestException = e }
                    };
                }
            }
        }

        // convenience methods
        public static async Task<HttpRequestResult<T>> SendAsync<T>(
            HttpClient client,
            HttpMethod method,
            string uri = null,
            HttpContent requestContent = null,
            IDictionary<string, string> headers = null,
            bool onlyParseBodyOnSuccess = true,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await HttpRequest.SendAsync((response) =>
            {
                return response.Content.ReadAsStringAsync();
            }, client, method, uri, requestContent, headers, onlyParseBodyOnSuccess, cancellationToken);

            return new HttpRequestResult<T>()
            {
                Status = result.Status,
                Value = (result.Status.Success || !onlyParseBodyOnSuccess) ? JsonConvert.DeserializeObject<T>(result.Value) : default(T)
            };
        }

        private static async Task<(HttpResponseMessage responseMessage, HttpRequestStatus status)> SendAsyncInternal(
            HttpClient client,
            HttpMethod method,
            string uri = null,
            HttpContent requestContent = null,
            IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var request = new HttpRequestMessage()
            {
                Method = method,
                RequestUri = GetFullUri(client, uri),
                Content = requestContent
            })
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                        request.Headers.Add(header.Key, header.Value);
                }

                HttpResponseMessage responseMessage;
                try
                {
                    responseMessage = await (cancellationToken.CanBeCanceled ? client.SendAsync(request, cancellationToken) : client.SendAsync(request));
                }
                catch (Exception e)
                {
                    ThrowIfNonHttpException(e, request.RequestUri.ToString());

                    Console.WriteLine($"Call to '{request.RequestUri}' failed with network exception {e}");
                    return (null, new HttpRequestStatus() { RequestException = e, ReasonPhrase = e.Message });
                }

                if (!responseMessage.IsSuccessStatusCode)
                {
                    var serverErrorString = responseMessage.StatusCode != HttpStatusCode.InternalServerError
                        ? await responseMessage.Content.ReadAsStringAsync() : null;
                    if (!string.IsNullOrEmpty(serverErrorString))
                        responseMessage.ReasonPhrase = serverErrorString.Replace("\n", ". ").Replace("\r", ". ");

                    Console.WriteLine($"Call to '{request.RequestUri}' failed with status code {responseMessage.StatusCode}. Reason: '{responseMessage.ReasonPhrase}'");
                    return (responseMessage, new HttpRequestStatus() { StatusCode = responseMessage.StatusCode, ReasonPhrase = responseMessage.ReasonPhrase });
                }

                return (responseMessage, new HttpRequestStatus() { StatusCode = responseMessage.StatusCode, Success = true });
            }
        }

        private static void ThrowIfNonHttpException(Exception e, string uri)
        {
            if (e is TimeoutException)
            {
                Console.WriteLine($"Error - Request to uri {uri} failed as the request timed out: {e}");
                return;
            }
            if (e is OperationCanceledException)
            {
                Console.WriteLine($"Error - Request to uri {uri} failed as the operation was canceled: {e}");
                return;
            }
            if (e is HttpRequestException)
            {
                Console.WriteLine($"Error - Request to uri {uri} failed: {e}");
                return;
            }

            throw e;
        }

        public static Uri GetFullUri(HttpClient client, string uri = null)
        {
            return client.BaseAddress != null
                ? (!string.IsNullOrEmpty(uri) ? new Uri(client.BaseAddress, uri) : client.BaseAddress)
                : new Uri(uri);
        }
    }
}
