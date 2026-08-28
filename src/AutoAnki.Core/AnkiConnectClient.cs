using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoAnki.Core;

public sealed partial class AnkiConnectClient(HttpClient httpClient, string endpoint) : IAnkiClient
{
    public const string NoteTypeName = "AutoAnki Vocabulary";
    private static readonly string[] ExpectedFields = ["Word", "Turkish", "Example"];
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await InvokeAsync("version", null, cancellationToken).ConfigureAwait(false);
            return result.ValueKind == JsonValueKind.Number && result.GetInt32() >= 5;
        }
        catch (Exception ex) when (ex is AnkiUnavailableException or AutoAnkiException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetDeckNamesAsync(CancellationToken cancellationToken)
    {
        var result = await InvokeAsync("deckNames", null, cancellationToken).ConfigureAwait(false);
        return result.EnumerateArray()
            .Select(item => item.GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task CreateDeckAsync(string deckName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deckName))
        {
            throw new AutoAnkiException("A target deck name is required.");
        }

        _ = await InvokeAsync("createDeck", new { deck = deckName.Trim() }, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureNoteTypeAsync(CancellationToken cancellationToken)
    {
        var namesResult = await InvokeAsync("modelNames", null, cancellationToken).ConfigureAwait(false);
        var names = namesResult.EnumerateArray().Select(item => item.GetString()).ToArray();

        if (names.Contains(NoteTypeName, StringComparer.Ordinal))
        {
            var fieldsResult = await InvokeAsync(
                "modelFieldNames",
                new { modelName = NoteTypeName },
                cancellationToken).ConfigureAwait(false);

            var existingFields = fieldsResult.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
            if (!existingFields.SequenceEqual(ExpectedFields, StringComparer.OrdinalIgnoreCase))
            {
                throw new AutoAnkiException(
                    $"The existing note type '{NoteTypeName}' is incompatible. Expected fields: {string.Join(", ", ExpectedFields)}.");
            }

            return;
        }

        var templates = new[]
        {
            new Dictionary<string, string>
            {
                ["Name"] = "Vocabulary Card",
                ["Front"] = "<div class=\"word\">{{Word}}</div>",
                ["Back"] = "{{FrontSide}}<hr id=\"answer\"><div class=\"translation\">{{Turkish}}</div><div class=\"example\">{{Example}}</div>"
            }
        };

        const string css = """
            .card { font-family: Segoe UI, Arial, sans-serif; font-size: 22px; text-align: center; color: #202124; background: #ffffff; padding: 24px; }
            .word { font-size: 34px; font-weight: 650; }
            .translation { font-size: 27px; font-weight: 600; margin: 18px 0 14px; color: #0b57d0; }
            .example { font-size: 21px; line-height: 1.45; color: #3c4043; }
            .nightMode .card, .night_mode .card { color: #e8eaed; background: #202124; }
            .nightMode .translation, .night_mode .translation { color: #8ab4f8; }
            .nightMode .example, .night_mode .example { color: #bdc1c6; }
            """;

        _ = await InvokeAsync(
            "createModel",
            new
            {
                modelName = NoteTypeName,
                inOrderFields = ExpectedFields,
                cardTemplates = templates,
                css,
                isCloze = false
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsDuplicateAsync(string deckName, string word, CancellationToken cancellationToken)
    {
        var probe = BuildNotePayload(deckName, word, "probe", "This is a probe example.", ["autoanki"]);
        var canAddResult = await InvokeAsync(
            "canAddNotesWithErrorDetail",
            new { notes = new[] { probe } },
            cancellationToken).ConfigureAwait(false);

        var detail = canAddResult[0];
        if (detail.TryGetProperty("canAdd", out var canAdd) && !canAdd.GetBoolean())
        {
            var error = detail.TryGetProperty("error", out var errorElement) ? errorElement.GetString() : null;
            if (error?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            throw new AutoAnkiException(error ?? "Anki rejected the note during duplicate validation.");
        }

        // Anki's checksum duplicate check is case-sensitive. Search and compare primary
        // fields to implement the app's stricter case-insensitive deck policy.
        var query = $"deck:\"{EscapeSearch(deckName)}\" \"{EscapeSearch(word)}\"";
        var idsResult = await InvokeAsync("findNotes", new { query }, cancellationToken).ConfigureAwait(false);
        var ids = idsResult.EnumerateArray().Select(item => item.GetInt64()).Take(250).ToArray();
        if (ids.Length == 0)
        {
            return false;
        }

        var notesResult = await InvokeAsync("notesInfo", new { notes = ids }, cancellationToken).ConfigureAwait(false);
        foreach (var note in notesResult.EnumerateArray())
        {
            if (!note.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var primaryValue = fields.EnumerateObject()
                .Select(property => property.Value)
                .Where(value => value.TryGetProperty("order", out var order) && order.GetInt32() == 0)
                .Select(value => value.TryGetProperty("value", out var fieldValue) ? fieldValue.GetString() : null)
                .FirstOrDefault();

            if (primaryValue is not null &&
                string.Equals(NormalizeField(primaryValue), NormalizeField(word), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<long> AddNoteAsync(AnkiNoteRequest note, CancellationToken cancellationToken)
    {
        var payload = BuildNotePayload(note.Deck, note.Word, note.Turkish, note.Example, note.Tags);
        var result = await InvokeAsync("addNote", new { note = payload }, cancellationToken).ConfigureAwait(false);
        return result.GetInt64();
    }

    private static object BuildNotePayload(
        string deck,
        string word,
        string turkish,
        string example,
        IReadOnlyList<string> tags) => new
        {
            deckName = deck,
            modelName = NoteTypeName,
            fields = new Dictionary<string, string>
            {
                ["Word"] = WebUtility.HtmlEncode(word),
                ["Turkish"] = WebUtility.HtmlEncode(turkish),
                ["Example"] = WebUtility.HtmlEncode(example)
            },
            tags,
            options = new
            {
                allowDuplicate = false,
                duplicateScope = "deck",
                duplicateScopeOptions = new
                {
                    deckName = deck,
                    checkChildren = false,
                    checkAllModels = true
                }
            }
        };

    private async Task<JsonElement> InvokeAsync(string action, object? parameters, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = action,
            ["version"] = 6,
            ["params"] = parameters ?? new { }
        }, JsonOptions);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);

        try
        {
            using var response = await httpClient.PostAsync(
                endpoint,
                new StringContent(payload, Encoding.UTF8, "application/json"),
                timeoutSource.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutSource.Token).ConfigureAwait(false);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
            {
                throw new AutoAnkiException(error.GetString() ?? $"AnkiConnect action '{action}' failed.");
            }

            if (!root.TryGetProperty("result", out var result))
            {
                throw new AutoAnkiException($"AnkiConnect action '{action}' returned no result.");
            }

            return result.Clone();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AnkiUnavailableException("Anki did not respond. Make sure Anki Desktop is open.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AnkiUnavailableException("AnkiConnect is unavailable. Make sure Anki Desktop is open.", ex);
        }
        catch (JsonException ex)
        {
            throw new AnkiUnavailableException("AnkiConnect returned an invalid response.", ex);
        }
    }

    private static string EscapeSearch(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string NormalizeField(string value)
    {
        var withoutHtml = HtmlTagRegex().Replace(value, string.Empty);
        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(withoutHtml), " ").Trim();
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
