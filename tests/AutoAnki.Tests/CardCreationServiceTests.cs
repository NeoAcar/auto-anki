using AutoAnki.Core;

namespace AutoAnki.Tests;

public sealed class CardCreationServiceTests
{
    [Fact]
    public async Task ClosedAnki_DoesNotCallProvidersOrAddNote()
    {
        var anki = new FakeAnki { Available = false };
        var translation = new CountingTranslation();
        var example = new CountingExample();
        var service = new CardCreationService(anki, translation, example);

        await Assert.ThrowsAsync<AnkiUnavailableException>(() =>
            service.CreateAsync("Deck", "reliable", CancellationToken.None));

        Assert.Equal(0, translation.CallCount);
        Assert.Equal(0, example.CallCount);
        Assert.Equal(0, anki.AddCount);
    }

    [Fact]
    public async Task Duplicate_DoesNotCallProvidersOrAddNote()
    {
        var anki = new FakeAnki { Available = true, Duplicate = true };
        var translation = new CountingTranslation();
        var example = new CountingExample();
        var service = new CardCreationService(anki, translation, example);

        await Assert.ThrowsAsync<DuplicateNoteException>(() =>
            service.CreateAsync("Deck", "reliable", CancellationToken.None));

        Assert.Equal(0, translation.CallCount);
        Assert.Equal(0, example.CallCount);
        Assert.Equal(0, anki.AddCount);
    }

    [Fact]
    public async Task Success_AddsExactlyOneCompleteNote()
    {
        var anki = new FakeAnki { Available = true };
        var service = new CardCreationService(anki, new CountingTranslation(), new CountingExample());

        var result = await service.CreateAsync("Deck", "reliable", CancellationToken.None);

        Assert.Equal(1, anki.AddCount);
        Assert.NotNull(anki.LastNote);
        Assert.Equal("reliable", anki.LastNote.Word);
        Assert.Equal("güvenilir", anki.LastNote.Turkish);
        Assert.Equal(result.ExampleSentence, anki.LastNote.Example);
    }

    private sealed class FakeAnki : IAnkiClient
    {
        public bool Available { get; init; }
        public bool Duplicate { get; init; }
        public int AddCount { get; private set; }
        public AnkiNoteRequest? LastNote { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(Available);
        public Task<IReadOnlyList<string>> GetDeckNamesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>(["Deck"]);
        public Task CreateDeckAsync(string deckName, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EnsureNoteTypeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsDuplicateAsync(string deckName, string word, CancellationToken cancellationToken) => Task.FromResult(Duplicate);
        public Task<long> AddNoteAsync(AnkiNoteRequest note, CancellationToken cancellationToken)
        {
            AddCount++;
            LastNote = note;
            return Task.FromResult(123L);
        }
    }

    private sealed class CountingTranslation : ITranslationProvider
    {
        public int CallCount { get; private set; }

        public Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new TranslationResult("güvenilir", "MyMemory"));
        }
    }

    private sealed class CountingExample : IExampleProvider
    {
        public int CallCount { get; private set; }

        public Task<ExampleGeneration> GenerateAsync(string term, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new ExampleGeneration("güvenilir", "She is a reliable friend in difficult situations."));
        }

        public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionTestResult(true, "ok"));
    }
}
