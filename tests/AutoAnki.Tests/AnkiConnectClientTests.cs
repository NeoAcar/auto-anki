using System.Text.Json;
using AutoAnki.Core;

namespace AutoAnki.Tests;

public sealed class AnkiConnectClientTests
{
    [Fact]
    public async Task IsDuplicate_RecognizesAnkiDuplicateDetail()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var action = ReadAction(request);
            return action switch
            {
                "canAddNotesWithErrorDetail" => FakeHttpMessageHandler.Json(
                    """{"result":[{"canAdd":false,"error":"cannot create note because it is a duplicate"}],"error":null}"""),
                _ => throw new InvalidOperationException(action)
            };
        });
        var client = new AnkiConnectClient(new HttpClient(handler), "http://anki.test");

        Assert.True(await client.IsDuplicateAsync("English", "Reliable", CancellationToken.None));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task EnsureNoteType_ValidatesExistingFields()
    {
        var handler = new FakeHttpMessageHandler(request => ReadAction(request) switch
        {
            "modelNames" => FakeHttpMessageHandler.Json("""{"result":["AutoAnki Vocabulary"],"error":null}"""),
            "modelFieldNames" => FakeHttpMessageHandler.Json("""{"result":["Word","Turkish","Example"],"error":null}"""),
            var action => throw new InvalidOperationException(action)
        });
        var client = new AnkiConnectClient(new HttpClient(handler), "http://anki.test");

        await client.EnsureNoteTypeAsync(CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task AddNote_HtmlEncodesAllUserContent()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return FakeHttpMessageHandler.Json("""{"result":12345,"error":null}""");
        });
        var client = new AnkiConnectClient(new HttpClient(handler), "http://anki.test");

        var id = await client.AddNoteAsync(
            new AnkiNoteRequest("Deck", "<word>", "çeviri & anlam", "Use <word> safely.", ["autoanki"]),
            CancellationToken.None);

        Assert.Equal(12345, id);
        using var document = JsonDocument.Parse(body!);
        var fields = document.RootElement.GetProperty("params").GetProperty("note").GetProperty("fields");
        Assert.Equal("&lt;word&gt;", fields.GetProperty("Word").GetString());
        Assert.Equal("&#231;eviri &amp; anlam", fields.GetProperty("Turkish").GetString());
        Assert.Equal("Use &lt;word&gt; safely.", fields.GetProperty("Example").GetString());
    }

    private static string ReadAction(HttpRequestMessage request)
    {
        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("action").GetString()!;
    }
}
