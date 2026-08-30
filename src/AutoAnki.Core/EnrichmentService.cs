namespace AutoAnki.Core;

public sealed class EnrichmentService(ITranslationProvider translationProvider, IExampleProvider exampleProvider)
{
    public async Task<EnrichmentResult> EnrichAsync(string term, CancellationToken cancellationToken)
    {
        var translationTask = TryTranslateAsync(term, cancellationToken);
        var exampleTask = exampleProvider.GenerateAsync(term, cancellationToken);

        await Task.WhenAll(translationTask, exampleTask).ConfigureAwait(false);

        var translation = await translationTask.ConfigureAwait(false);
        var generated = await exampleTask.ConfigureAwait(false);
        // Gemini already produces a translation as part of the example-generation request.
        // It is substantially more reliable for vocabulary than MyMemory's occasional
        // transliterations (for example, returning an English word written phonetically).
        var turkish = generated.TurkishTranslation;
        var provider = "Gemini";

        if (string.IsNullOrWhiteSpace(turkish))
        {
            turkish = translation?.Text;
            provider = translation?.Provider;
        }

        if (string.IsNullOrWhiteSpace(turkish) || string.IsNullOrWhiteSpace(generated.ExampleSentence))
        {
            throw new ProviderException("The enrichment providers returned incomplete content.");
        }

        return new EnrichmentResult(turkish.Trim(), generated.ExampleSentence.Trim(), provider ?? "Unknown");
    }

    private async Task<TranslationResult?> TryTranslateAsync(string term, CancellationToken cancellationToken)
    {
        try
        {
            return await translationProvider.TranslateAsync(term, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderException)
        {
            return null;
        }
    }
}
