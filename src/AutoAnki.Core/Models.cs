namespace AutoAnki.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 3;
    public const string DefaultGeminiModel = "gemini-3.5-flash-lite";
    public const string DefaultGeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";
    public const string DefaultMyMemoryEndpoint = "https://api.mymemory.translated.net/get";
    public const string DefaultAnkiEndpoint = "http://127.0.0.1:8765";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string TargetDeck { get; init; } = string.Empty;
    public int HotkeyModifiers { get; init; } = 0x0002 | 0x0004; // Ctrl + Shift
    public int HotkeyVirtualKey { get; init; } = 0x7B; // F12
    public bool StartWithWindows { get; init; } = true;
    public string GeminiModel { get; init; } = DefaultGeminiModel;
    public string GeminiEndpoint { get; init; } = DefaultGeminiEndpoint;
    public string MyMemoryEndpoint { get; init; } = DefaultMyMemoryEndpoint;
    public string AnkiEndpoint { get; init; } = DefaultAnkiEndpoint;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TargetDeck);
}

public enum SelectionCaptureError
{
    None,
    ClipboardUnavailable,
    NoSelection,
    TooLong,
    TooManyWords
}

public sealed record SelectionCaptureResult(string? Text, SelectionCaptureError Error)
{
    public bool IsSuccess => Error == SelectionCaptureError.None && !string.IsNullOrWhiteSpace(Text);

    public static SelectionCaptureResult Success(string text) => new(text, SelectionCaptureError.None);
    public static SelectionCaptureResult Failure(SelectionCaptureError error) => new(null, error);
}

public sealed record TranslationResult(string Text, string Provider);
public sealed record ExampleGeneration(string TurkishTranslation, string ExampleSentence);
public sealed record EnrichmentResult(string TurkishTranslation, string ExampleSentence, string TranslationProvider);

public sealed record AnkiNoteRequest(
    string Deck,
    string Word,
    string Turkish,
    string Example,
    IReadOnlyList<string> Tags);

public sealed record ConnectionTestResult(bool Success, string Message);

public class AutoAnkiException : Exception
{
    public AutoAnkiException(string message) : base(message) { }
    public AutoAnkiException(string message, Exception innerException) : base(message, innerException) { }
}

public class ProviderException : AutoAnkiException
{
    public ProviderException(string message) : base(message) { }
    public ProviderException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class AnkiUnavailableException : AutoAnkiException
{
    public AnkiUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException ?? new InvalidOperationException(message)) { }
}

public sealed class DuplicateNoteException : AutoAnkiException
{
    public DuplicateNoteException() : base("This word or phrase already exists in the target deck.") { }
}
