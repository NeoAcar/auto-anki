namespace AutoAnki.Core;

public interface ITranslationProvider
{
    Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken);
}

public interface IExampleProvider
{
    Task<ExampleGeneration> GenerateAsync(string term, CancellationToken cancellationToken);
    Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken);
}

public interface IAnkiClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDeckNamesAsync(CancellationToken cancellationToken);
    Task CreateDeckAsync(string deckName, CancellationToken cancellationToken);
    Task EnsureNoteTypeAsync(CancellationToken cancellationToken);
    Task<bool> IsDuplicateAsync(string deckName, string word, CancellationToken cancellationToken);
    Task<long> AddNoteAsync(AnkiNoteRequest note, CancellationToken cancellationToken);
}
