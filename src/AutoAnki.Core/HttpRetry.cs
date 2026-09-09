using System.Net;

namespace AutoAnki.Core;

internal static class HttpRetry
{
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = requestFactory();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token)
                    .ConfigureAwait(false);

                if (!IsTransient(response.StatusCode) || attempt == 1)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException("The request timed out.");
                if (attempt == 1)
                {
                    break;
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                if (attempt == 1)
                {
                    break;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(attempt == 0 ? 250 : 750), cancellationToken)
                .ConfigureAwait(false);
        }

        throw new ProviderException("The remote service could not be reached.", lastException ?? new HttpRequestException());
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;
}
