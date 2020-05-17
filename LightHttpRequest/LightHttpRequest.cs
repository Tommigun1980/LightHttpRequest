using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;

public static class LightHttpRequest
{
    public class RequestStatus
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

    public class RequestResult<T>
    {
        public RequestStatus Status { get; set; }

        // Result is available if Status.Success is true
        public T Value { get; set; }
    }

    public static async Task<RequestStatus> SendAsync(
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var result = await LightHttpRequest.SendAsyncInternal(client, method, uri, requestContent, headers, cancellationToken);
        using (result.responseMessage)
        {
            return result.status;
        }
    }

    public static async Task<RequestResult<T>> SendAsync<T>(
        Func<HttpResponseMessage, Task<T>> responseHandler,
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var result = await LightHttpRequest.SendAsyncInternal(client, method, uri, requestContent, headers, cancellationToken);
        using (result.responseMessage)
        {
            if (!result.status.Success)
                return new RequestResult<T>() { Status = result.status };

            try
            {
                var convertedValue = await responseHandler(result.responseMessage);
                return new RequestResult<T>() { Value = convertedValue, Status = result.status };
            }
            catch (Exception e)
            {
                var fullUri = GetFullUri(client, uri);
                Console.WriteLine($"Error - Object conversion failed for {fullUri}: {e}");
                ThrowIfNonHttpException(e, fullUri.ToString());

                return new RequestResult<T>() {
                    Status = new RequestStatus() { RequestException = e }
                };
            }
        }
    }

    // convenience methods
    public static async Task<RequestResult<T>> SendAsync<T>(
        HttpClient client,
        HttpMethod method,
        string uri = null,
        HttpContent requestContent = null,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var result = await LightHttpRequest.SendAsync((response) =>
        {
            return response.Content.ReadAsStringAsync();
        }, client, method, uri, requestContent, headers, cancellationToken);

        return new RequestResult<T>()
        {
            Status = result.Status,
            Value = result.Status.Success ? JsonConvert.DeserializeObject<T>(result.Value) : default(T)
        };
    }

    private static async Task<(HttpResponseMessage responseMessage, RequestStatus status)> SendAsyncInternal(
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
                return (null, new RequestStatus() { RequestException = e, ReasonPhrase = e.Message });
            }

            if (!responseMessage.IsSuccessStatusCode)
            {
                using (responseMessage)
                {
                    var serverErrorString = responseMessage.StatusCode != HttpStatusCode.InternalServerError
                        ? await responseMessage.Content.ReadAsStringAsync() : null;
                    if (!string.IsNullOrEmpty(serverErrorString))
                        responseMessage.ReasonPhrase = serverErrorString;

                    Console.WriteLine($"Call to '{request.RequestUri}' failed with status code {responseMessage.StatusCode}. Reason: '{responseMessage.ReasonPhrase}'");
                    return (null, new RequestStatus() { StatusCode = responseMessage.StatusCode, ReasonPhrase = serverErrorString });
                }
            }

            return (responseMessage, new RequestStatus() { Success = true });
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
