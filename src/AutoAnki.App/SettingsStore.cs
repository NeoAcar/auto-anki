using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoAnki.Core;

namespace AutoAnki.App;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string settingsPath;
    private readonly string secretsPath;

    public SettingsStore(string? dataDirectory = null)
    {
        var directory = dataDirectory ?? AppPaths.DataDirectory;
        Directory.CreateDirectory(directory);
        settingsPath = Path.Combine(directory, "settings.json");
        secretsPath = Path.Combine(directory, "secrets.dat");
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath), JsonOptions);
            if (settings is null)
            {
                return new AppSettings();
            }

            if (settings.SchemaVersion < AppSettings.CurrentSchemaVersion)
            {
                settings = settings with
                {
                    SchemaVersion = AppSettings.CurrentSchemaVersion,
                    // Version 1's dropdown could display F12 while saving F1.
                    HotkeyVirtualKey = settings.HotkeyVirtualKey == 0x70 ? 0x7B : settings.HotkeyVirtualKey,
                    // Flash-Lite is faster and remained available when 3.7 Flash was overloaded.
                    GeminiModel = settings.GeminiModel.Equals(
                        "gemini-3.7-flash",
                        StringComparison.OrdinalIgnoreCase)
                            ? AppSettings.DefaultGeminiModel
                            : settings.GeminiModel
                };
                SaveSettingsFile(settings);
            }

            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public string LoadApiKey()
    {
        if (!File.Exists(secretsPath))
        {
            return string.Empty;
        }

        try
        {
            var encrypted = File.ReadAllBytes(secretsPath);
            var clear = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clear);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    public void Save(AppSettings settings, string apiKey)
    {
        var tempSettings = settingsPath + ".tmp";
        var clear = Encoding.UTF8.GetBytes(apiKey.Trim());
        try
        {
            var encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
            var tempSecrets = secretsPath + ".tmp";
            File.WriteAllBytes(tempSecrets, encrypted);
            File.WriteAllText(tempSettings, JsonSerializer.Serialize(settings, JsonOptions), new UTF8Encoding(false));
            File.Move(tempSecrets, secretsPath, true);
            File.Move(tempSettings, settingsPath, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
        }
    }

    private void SaveSettingsFile(AppSettings settings)
    {
        var tempSettings = settingsPath + ".tmp";
        File.WriteAllText(
            tempSettings,
            JsonSerializer.Serialize(settings, JsonOptions),
            new UTF8Encoding(false));
        File.Move(tempSettings, settingsPath, true);
    }
}
