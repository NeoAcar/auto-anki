namespace AutoAnki.Core;

public sealed class CardCreationService(
    IAnkiClient ankiClient,
    ITranslationProvider translationProvider,
    IExampleProvider exampleProvider)
{
    public async Task<EnrichmentResult> CreateAsync(
        string deckName,
        string term,
        CancellationToken cancellationToken)
    {
        if (!await ankiClient.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new AnkiUnavailableException("Open Anki Desktop and try again. The card was not queued.");
        }

        await ankiClient.EnsureNoteTypeAsync(cancellationToken).ConfigureAwait(false);
        if (await ankiClient.IsDuplicateAsync(deckName, term, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicateNoteException();
        }

        var enrichment = await new EnrichmentService(translationProvider, exampleProvider)
            .EnrichAsync(term, cancellationToken).ConfigureAwait(false);

        var request = new AnkiNoteRequest(
            deckName,
            term,
            enrichment.TurkishTranslation,
            enrichment.ExampleSentence,
            ["autoanki"]);

        _ = await ankiClient.AddNoteAsync(request, cancellationToken).ConfigureAwait(false);
        return enrichment;
    }
}
