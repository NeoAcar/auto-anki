using AutoAnki.App;
using AutoAnki.Core;

namespace AutoAnki.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AutoAnkiTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsSettingsAndEncryptedSecret()
    {
        var store = new SettingsStore(testDirectory);
        var settings = new AppSettings { TargetDeck = "English", StartWithWindows = false };

        store.Save(settings, "test-api-key");

        Assert.Equal("English", store.LoadSettings().TargetDeck);
        Assert.Equal("test-api-key", store.LoadApiKey());
        Assert.DoesNotContain("test-api-key", File.ReadAllText(Path.Combine(testDirectory, "settings.json")), StringComparison.Ordinal);
        Assert.DoesNotContain("test-api-key", Convert.ToBase64String(File.ReadAllBytes(Path.Combine(testDirectory, "secrets.dat"))), StringComparison.Ordinal);
    }

    [Fact]
    public void LoadSettings_MigratesVersionOneF1BindingBugToF12()
    {
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(
            Path.Combine(testDirectory, "settings.json"),
            """{"schemaVersion":1,"targetDeck":"AutoAnki","hotkeyModifiers":6,"hotkeyVirtualKey":112,"startWithWindows":true}""");
        var store = new SettingsStore(testDirectory);

        var settings = store.LoadSettings();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(123, settings.HotkeyVirtualKey);
        Assert.Equal(AppSettings.DefaultGeminiModel, settings.GeminiModel);
        Assert.Contains("\"hotkeyVirtualKey\": 123", File.ReadAllText(Path.Combine(testDirectory, "settings.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void LoadSettings_MigratesGemini37ToFlashLite()
    {
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(
            Path.Combine(testDirectory, "settings.json"),
            """{"schemaVersion":2,"targetDeck":"AutoAnki","hotkeyModifiers":6,"hotkeyVirtualKey":122,"startWithWindows":true,"geminiModel":"gemini-3.7-flash"}""");
        var store = new SettingsStore(testDirectory);

        var settings = store.LoadSettings();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(AppSettings.DefaultGeminiModel, settings.GeminiModel);
        Assert.Equal(122, settings.HotkeyVirtualKey);
    }

    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}
