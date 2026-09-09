using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoAnki.Core;

public sealed partial class GeminiExampleProvider(
    HttpClient httpClient,
    string endpoint,
    string apiKey,
    string model) : IExampleProvider
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ExampleGeneration> GenerateAsync(string term, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderException("A Gemini API key is required.");
        }

        GeminiApiException? lastCapacityError = null;
        foreach (var candidateModel in GetModelCandidates())
        {
            try
            {
                return await GenerateWithValidationAsync(term, candidateModel, cancellationToken).ConfigureAwait(false);
            }
            catch (GeminiApiException ex) when (IsCapacityError(ex.StatusCode))
            {
                lastCapacityError = ex;
            }
        }

        throw lastCapacityError ?? new ProviderException("No Gemini model was available.");
    }

    private async Task<ExampleGeneration> GenerateWithValidationAsync(
        string term,
        string candidateModel,
        CancellationToken cancellationToken)
    {
        GeminiOutputValidationException? lastError = null;
        for (var validationAttempt = 0; validationAttempt < 2; validationAttempt++)
        {
            try
            {
                var generated = await GenerateOnceAsync(
                    term,
                    candidateModel,
                    validationAttempt > 0,
                    cancellationToken).ConfigureAwait(false);
                Validate(term, generated);
                return generated;
            }
            catch (GeminiOutputValidationException ex)
            {
                lastError = ex;
            }
        }

        throw new ProviderException(
            $"Gemini returned invalid card content after one retry: {lastError?.Message ?? "unknown validation error"}",
            lastError!);
    }

    public async Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await GenerateAsync("reliable", cancellationToken).ConfigureAwait(false);
            return new ConnectionTestResult(true, "Gemini connection and API key are working.");
        }
        catch (Exception ex) when (ex is ProviderException or HttpRequestException)
        {
            return new ConnectionTestResult(false, ex.Message);
        }
    }

    private async Task<ExampleGeneration> GenerateOnceAsync(
        string term,
        string candidateModel,
        bool isRepairAttempt,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(term, isRepairAttempt);
        var payload = new
        {
            model = candidateModel,
            store = false,
            generation_config = new
            {
                thinking_level = "low",
                max_output_tokens = 200
            },
            input = prompt,
            response_format = new
            {
                type = "text",
                mime_type = "application/json",
                schema = new
                {
                    type = "object",
                    properties = new
                    {
                        turkishTranslation = new
                        {
                            type = "string",
                            description = "A concise Turkish translation of the supplied English word or phrase."
                        },
                        exampleSentence = new
                        {
                            type = "string",
                            description = "Exactly one natural B1-B2 English sentence containing the supplied term verbatim."
                        }
                    },
                    required = new[] { "turkishTranslation", "exampleSentence" }
                }
            }
        };

        var body = JsonSerializer.Serialize(payload, JsonOptions);
        using var response = await HttpRetry.SendAsync(
            httpClient,
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Headers.Add("Api-Revision", "2026-05-20");
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return request;
            },
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = ExtractApiError(responseBody);
            throw new GeminiApiException(
                response.StatusCode,
                $"Gemini returned HTTP {(int)response.StatusCode} from {candidateModel}{detail}.");
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var text = FindModelOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new GeminiOutputValidationException("the response contained no text output");
            }

            var result = JsonSerializer.Deserialize<ExampleGeneration>(text, JsonOptions);
            return result ?? throw new GeminiOutputValidationException("the structured output was empty");
        }
        catch (JsonException ex)
        {
            throw new GeminiOutputValidationException("the structured JSON could not be parsed", ex);
        }
    }

    private static string BuildPrompt(string term, bool isRepairAttempt) => $"""
        Create a vocabulary card for the exact English word or phrase below.

        Term: {term}

        Return a concise Turkish translation and exactly one natural, everyday B1-B2 English example sentence.
        The example must contain the exact term "{term}" verbatim, ignoring capitalization.
        Use 5-30 words. Add no definitions, labels, quotation marks, Markdown, or extra sentences.
        {(isRepairAttempt ? "This is a repair attempt: follow every constraint exactly." : string.Empty)}
        """;

    private static void Validate(string term, ExampleGeneration generated)
    {
        if (string.IsNullOrWhiteSpace(generated.ExampleSentence))
        {
            throw new GeminiOutputValidationException("the example was empty");
        }

        var sentence = generated.ExampleSentence.Trim();
        if (sentence.Contains('\n') || !ContainsExactTerm(sentence, term))
        {
            throw new GeminiOutputValidationException("the example did not contain the selected term exactly");
        }

        var wordCount = WhitespaceRegex().Split(sentence).Count(part => part.Length > 0);
        if (wordCount is < 5 or > 30)
        {
            throw new GeminiOutputValidationException("the example was outside the 5-30 word limit");
        }

        var sentenceEndCount = SentenceEndRegex().Matches(sentence).Count;
        if (sentenceEndCount > 1)
        {
            throw new GeminiOutputValidationException("the example contained more than one sentence");
        }
    }

    private static bool ContainsExactTerm(string sentence, string term)
    {
        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term.Trim())}(?![\p{{L}}\p{{N}}])";
        return Regex.IsMatch(sentence, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string? FindModelOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var step in steps.EnumerateArray())
        {
            if (!step.TryGetProperty("type", out var type) || type.GetString() != "model_output" ||
                !step.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var blockType) && blockType.GetString() == "text" &&
                    block.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static string ExtractApiError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var message = document.RootElement.GetProperty("error").GetProperty("message").GetString();
            return string.IsNullOrWhiteSpace(message) ? string.Empty : $": {message}";
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[.!?](?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceEndRegex();

    private IReadOnlyList<string> GetModelCandidates() =>
        new[] { model, "gemini-3.5-flash-lite", "gemini-3.1-flash-lite" }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsCapacityError(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    private sealed class GeminiApiException(HttpStatusCode statusCode, string message) : ProviderException(message)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }

    private sealed class GeminiOutputValidationException : ProviderException
    {
        public GeminiOutputValidationException(string message) : base(message) { }
        public GeminiOutputValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
