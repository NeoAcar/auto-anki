using System.Net;
using AutoAnki.Core;

namespace AutoAnki.Tests;

public sealed class ProviderTests
{
    [Fact]
    public async Task MyMemory_ParsesAndDecodesTranslation()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(
            """{"responseData":{"translatedText":"güvenilir &amp; sağlam"}}"""));
        var provider = new MyMemoryTranslationProvider(new HttpClient(handler), "https://translation.test/get");

        var result = await provider.TranslateAsync("reliable", CancellationToken.None);

        Assert.Equal("güvenilir & sağlam", result.Text);
        Assert.Equal("MyMemory", result.Provider);
    }

    [Fact]
    public async Task MyMemory_TreatsErrorStatusInsideHttpSuccessAsFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(
            """{"responseStatus":429,"responseData":{"translatedText":"QUERY LENGTH LIMIT EXCEEDED"}}"""));
        var provider = new MyMemoryTranslationProvider(new HttpClient(handler), "https://translation.test/get");

        await Assert.ThrowsAsync<ProviderException>(() =>
            provider.TranslateAsync("reliable", CancellationToken.None));
    }

    [Fact]
    public async Task Gemini_ParsesStructuredInteractionOutput()
    {
        const string response = """
            {
              "status":"completed",
              "steps":[{"type":"model_output","content":[{"type":"text","text":"{\"turkishTranslation\":\"güvenilir\",\"exampleSentence\":\"She is a reliable friend in difficult situations.\"}"}]}]
            }
            """;
        string? requestBody = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return FakeHttpMessageHandler.Json(response);
        });
        var provider = new GeminiExampleProvider(new HttpClient(handler), "https://gemini.test/interactions", "secret", "test-model");

        var result = await provider.GenerateAsync("reliable", CancellationToken.None);

        Assert.Equal("güvenilir", result.TurkishTranslation);
        Assert.Contains("reliable", result.ExampleSentence, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("\"generation_config\":{\"thinking_level\":\"low\"", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gemini_RetriesOnceWhenTermIsMissing()
    {
        var count = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            count++;
            var sentence = count == 1
                ? "She always helps her friends in difficult situations."
                : "She is a reliable friend in difficult situations.";
            var escaped = sentence.Replace("\"", "\\\"", StringComparison.Ordinal);
            return FakeHttpMessageHandler.Json(
                $$"""{"status":"completed","steps":[{"type":"model_output","content":[{"type":"text","text":"{\"turkishTranslation\":\"güvenilir\",\"exampleSentence\":\"{{escaped}}\"}"}]}]}""");
        });
        var provider = new GeminiExampleProvider(new HttpClient(handler), "https://gemini.test/interactions", "secret", "test-model");

        var result = await provider.GenerateAsync("reliable", CancellationToken.None);

        Assert.Contains("reliable", result.ExampleSentence, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Gemini_DoesNotMaskOrRetryApiErrorsAsContentValidation()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(
            """{"error":{"message":"API key not valid. Please pass a valid API key."}}""",
            HttpStatusCode.BadRequest));
        var provider = new GeminiExampleProvider(
            new HttpClient(handler),
            "https://gemini.test/interactions",
            "bad-key",
            "test-model");

        var error = await Assert.ThrowsAnyAsync<ProviderException>(() =>
            provider.GenerateAsync("reliable", CancellationToken.None));

        Assert.Contains("API key not valid", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("valid example", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Gemini_FallsBackToFlashLiteWhenPrimaryModelIsOverloaded()
    {
        var requestedModels = new List<string>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var document = System.Text.Json.JsonDocument.Parse(body);
            var requestedModel = document.RootElement.GetProperty("model").GetString()!;
            requestedModels.Add(requestedModel);

            if (requestedModel == "gemini-3.7-flash")
            {
                return FakeHttpMessageHandler.Json(
                    """{"error":{"message":"model is currently experiencing high demand"}}""",
                    HttpStatusCode.InternalServerError);
            }

            return FakeHttpMessageHandler.Json(
                """{"status":"completed","steps":[{"type":"model_output","content":[{"type":"text","text":"{\"turkishTranslation\":\"güvenilir\",\"exampleSentence\":\"She is a reliable friend in difficult situations.\"}"}]}]}""");
        });
        var provider = new GeminiExampleProvider(
            new HttpClient(handler),
            "https://gemini.test/interactions",
            "valid-key",
            "gemini-3.7-flash");

        var result = await provider.GenerateAsync("reliable", CancellationToken.None);

        Assert.Equal("güvenilir", result.TurkishTranslation);
        Assert.Equal(3, handler.RequestCount); // two transient retries, then fallback
        Assert.Equal(
            ["gemini-3.7-flash", "gemini-3.7-flash", "gemini-3.5-flash-lite"],
            requestedModels);
    }

    [Fact]
    public async Task Enrichment_FallsBackToGeminiTranslation()
    {
        var service = new EnrichmentService(new FailingTranslation(), new FixedExample());

        var result = await service.EnrichAsync("reliable", CancellationToken.None);

        Assert.Equal("güvenilir", result.TurkishTranslation);
        Assert.Equal("Gemini fallback", result.TranslationProvider);
    }

    private sealed class FailingTranslation : ITranslationProvider
    {
        public Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken) =>
            Task.FromException<TranslationResult>(new ProviderException("unavailable"));
    }

    private sealed class FixedExample : IExampleProvider
    {
        public Task<ExampleGeneration> GenerateAsync(string term, CancellationToken cancellationToken) =>
            Task.FromResult(new ExampleGeneration("güvenilir", "She is a reliable friend in difficult situations."));

        public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionTestResult(true, "ok"));
    }
}
