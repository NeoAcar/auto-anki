using System.Net;
using System.Text.Json;

namespace AutoAnki.Core;

public sealed class MyMemoryTranslationProvider(HttpClient httpClient, string endpoint) : ITranslationProvider
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public async Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var uri = $"{endpoint}{separator}q={Uri.EscapeDataString(text)}&langpair=en%7Ctr&mt=1";

        using var response = await HttpRetry.SendAsync(
            httpClient,
            () => new HttpRequestMessage(HttpMethod.Get, uri),
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderException($"MyMemory returned HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("responseStatus", out var statusElement))
        {
            var status = statusElement.ValueKind switch
            {
                JsonValueKind.Number => statusElement.GetInt32(),
                JsonValueKind.String when int.TryParse(statusElement.GetString(), out var parsed) => parsed,
                _ => 200
            };
            if (status >= 400)
            {
                throw new ProviderException($"MyMemory rejected the request with status {status}.");
            }
        }

        if (!document.RootElement.TryGetProperty("responseData", out var responseData) ||
            !responseData.TryGetProperty("translatedText", out var translatedElement))
        {
            throw new ProviderException("MyMemory returned an unexpected response.");
        }

        var translated = WebUtility.HtmlDecode(translatedElement.GetString())?.Trim();
        if (string.IsNullOrWhiteSpace(translated))
        {
            throw new ProviderException("MyMemory returned an empty translation.");
        }

        return new TranslationResult(translated, "MyMemory");
    }
}
