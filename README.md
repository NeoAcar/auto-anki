# AutoAnki

AutoAnki is a native Windows tray app that turns highlighted English words or short phrases into Anki vocabulary cards. Select text in Chrome, YouTube, WhatsApp, Notepad, or another copy-capable app and press the configured shortcut.

The generated card contains:

- Front: selected word or phrase
- Back: Turkish translation and one natural B1–B2 English example sentence

## Requirements

- Windows 10 or 11, x64
- Anki Desktop running while cards are added
- [AnkiConnect](https://ankiweb.net/shared/info/2055492159) installed in Anki
- A free [Google AI Studio Gemini API key](https://aistudio.google.com/app/apikey)
- Internet access for MyMemory and Gemini

## Download the Windows app

Download the latest `AutoAnki-v*-win-x64.zip` from the [GitHub Releases page](https://github.com/NeoAcar/auto-anki/releases/latest), extract it, and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-AutoAnki.ps1
```

The release is self-contained, so users do not need to install .NET. AutoAnki is not code-signed yet, so Windows SmartScreen may show an Unknown Publisher warning. Review the source or build it locally if desired.

## Build and install

The repository targets .NET 8. Run from PowerShell:

```powershell
.\scripts\install.ps1
```

This runs all tests, creates a self-contained single-file executable, installs it under `%LOCALAPPDATA%\AutoAnki\app`, adds a Start Menu shortcut, and starts the first-run wizard. Keep Anki open during setup.

For development:

```powershell
dotnet test .\AutoAnki.sln
dotnet run --project .\src\AutoAnki.App\AutoAnki.App.csproj
```

To uninstall while preserving settings:

```powershell
.\scripts\uninstall.ps1
```

To remove settings and encrypted credentials as well:

```powershell
.\scripts\uninstall.ps1 -RemoveUserData
```

## Day-to-day use

1. Keep Anki Desktop open.
2. Highlight one English word or a phrase up to 12 words/120 characters.
3. Press the copy shortcut (`Ctrl+Shift+F12` by default).
4. If the text cannot be copied (for example, a video subtitle), press the OCR shortcut (`Ctrl+Shift+F10` by default), drag over the text, and choose the recognized word or phrase.
5. If neither is convenient, press the manual-entry shortcut (`Ctrl+Shift+F9` by default) and type it.
6. Wait for the visible AutoAnki popup and sound confirming the card was added.

Right-click the tray icon to add the current selection, change settings, test connections, pause the shortcut, or exit.

If another application already owns a shortcut, choose a different one in Settings. OCR runs locally through Windows English OCR; install the English language/OCR pack in Windows Settings if AutoAnki reports that no OCR language is available.

## Providers

AutoAnki checks Anki and duplicates before making internet requests. Gemini generates the example sentence and Turkish translation. MyMemory remains a fallback if Gemini returns no translation. `gemini-3.5-flash-lite` is the default, with another Flash-Lite model used if capacity errors occur.

## Privacy and limitations

The selected term is sent to MyMemory and Google Gemini. The Gemini key is encrypted with Windows DPAPI for the current user. Logs contain error categories but not selected text, card content, clipboard data, or keys.

Capture uses the foreground application's normal Copy command, or a local full-screen OCR selection overlay when copying is unavailable. OCR cannot read text that is too small, blurred, or hidden behind protected/UAC screens. Password fields, protected content, and apps running at a higher privilege level than AutoAnki may still block capture. If Anki is closed, the card is not queued.

## License

[MIT](LICENSE)
