using System.ComponentModel;
using AutoAnki.Core;

namespace AutoAnki.App;

internal sealed class AutoAnkiApplicationContext : ApplicationContext
{
    private readonly HttpClient httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly SettingsStore settingsStore = new();
    private readonly FileLogger logger = new();
    private readonly GlobalHotkeyWindow hotkeyWindow = new();
    private readonly ClipboardCaptureService clipboardCapture = new();
    private readonly CancellationTokenSource shutdown = new();
    private readonly Control dispatcher = new();
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem pauseItem;
    private readonly EventWaitHandle settingsEvent;
    private AppSettings settings;
    private string apiKey;
    private AppToastForm? activeToast;
    private int busy;
    private bool exiting;

    public AutoAnkiApplicationContext(EventWaitHandle settingsEvent)
    {
        this.settingsEvent = settingsEvent;
        settings = settingsStore.LoadSettings();
        apiKey = settingsStore.LoadApiKey();
        dispatcher.CreateControl();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Add current selection", null, async (_, _) => await CaptureAndAddAsync());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings(firstRun: false));
        menu.Items.Add("Test connections", null, async (_, _) => await TestConnectionsAsync());
        pauseItem = new ToolStripMenuItem("Pause shortcut", null, (_, _) => TogglePause());
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AutoAnki",
            Visible = true,
            ContextMenuStrip = menu
        };
        trayIcon.DoubleClick += (_, _) => ShowSettings(firstRun: false);
        hotkeyWindow.Pressed += async (_, _) =>
        {
            logger.Info("hotkey", "Shortcut activated.");
            await CaptureAndAddAsync();
        };

        if (settings.IsConfigured && !string.IsNullOrWhiteSpace(apiKey))
        {
            TryRegisterConfiguredHotkey(showError: true);
            StartupManager.SetEnabled(settings.StartWithWindows);
        }
        else
        {
            dispatcher.BeginInvoke(() => ShowSettings(firstRun: true));
        }

        _ = WatchSettingsEventAsync();
        logger.Info("lifecycle", "AutoAnki started.");
    }

    private async Task CaptureAndAddAsync()
    {
        if (Interlocked.Exchange(ref busy, 1) != 0)
        {
            Notify("AutoAnki is busy", "Wait for the current card to finish.", ToolTipIcon.Info);
            return;
        }

        try
        {
            if (!settings.IsConfigured || string.IsNullOrWhiteSpace(apiKey))
            {
                Notify("Setup required", "Open AutoAnki Settings and finish setup.", ToolTipIcon.Warning);
                return;
            }

            var capture = await clipboardCapture.CaptureAsync(shutdown.Token);
            if (!capture.IsSuccess)
            {
                logger.Info("capture", $"Selection rejected: {capture.Error}.");
                NotifyCaptureError(capture.Error);
                return;
            }

            var term = capture.Text!;
            logger.Info("capture", "Valid selection captured.");
            var anki = CreateAnkiClient();
            var translation = new MyMemoryTranslationProvider(httpClient, settings.MyMemoryEndpoint);
            var example = new GeminiExampleProvider(httpClient, settings.GeminiEndpoint, apiKey, settings.GeminiModel);
            var enrichment = await new CardCreationService(anki, translation, example)
                .CreateAsync(settings.TargetDeck, term, shutdown.Token);
            logger.Info("card", $"Card added using {enrichment.TranslationProvider} translation.");
            Notify("Card added", $"Added “{term}” to {settings.TargetDeck}.", ToolTipIcon.Info);
        }
        catch (DuplicateNoteException)
        {
            logger.Info("card", "Duplicate selection skipped.");
            Notify("Already exists", "That word or phrase is already in the target deck.", ToolTipIcon.Info);
        }
        catch (AnkiUnavailableException ex)
        {
            logger.Error("anki-unavailable", ex);
            Notify("Anki is unavailable", ex.Message, ToolTipIcon.Error);
        }
        catch (ProviderException ex)
        {
            logger.Error("provider", ex);
            Notify("Could not create card", ex.Message, ToolTipIcon.Error);
        }
        catch (AutoAnkiException ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            Notify("Already exists", "That word or phrase is already in the target deck.", ToolTipIcon.Info);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.Error("unexpected", ex);
            Notify("AutoAnki error", "The card was not added. See the local log for details.", ToolTipIcon.Error);
        }
        finally
        {
            Interlocked.Exchange(ref busy, 0);
        }
    }

    private void NotifyCaptureError(SelectionCaptureError error)
    {
        var message = error switch
        {
            SelectionCaptureError.TooLong => "Select at most 120 characters.",
            SelectionCaptureError.TooManyWords => "Select at most 12 words.",
            SelectionCaptureError.ClipboardUnavailable => "Windows could not access the clipboard. Try again.",
            _ => "No copied text was detected. Highlight a word or short phrase and try again."
        };
        Notify("No valid selection", message, ToolTipIcon.Warning);
    }

    private async Task TestConnectionsAsync()
    {
        if (Interlocked.Exchange(ref busy, 1) != 0)
        {
            Notify("AutoAnki is busy", "Wait for the current operation to finish.", ToolTipIcon.Info);
            return;
        }

        try
        {
            var ankiOk = await CreateAnkiClient().IsAvailableAsync(shutdown.Token);
            if (!ankiOk)
            {
                Notify("Connection test", "Anki is unavailable. Open Anki Desktop.", ToolTipIcon.Error);
                return;
            }

            var gemini = new GeminiExampleProvider(httpClient, settings.GeminiEndpoint, apiKey, settings.GeminiModel);
            var geminiResult = await gemini.TestAsync(shutdown.Token);
            Notify(
                "Connection test",
                geminiResult.Success ? "Anki and Gemini are working." : geminiResult.Message,
                geminiResult.Success ? ToolTipIcon.Info : ToolTipIcon.Error);
        }
        catch (Exception ex) when (ex is AutoAnkiException or HttpRequestException or UriFormatException)
        {
            logger.Error("connection-test", ex);
            Notify("Connection test failed", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            Interlocked.Exchange(ref busy, 0);
        }
    }

    private void ShowSettings(bool firstRun)
    {
        if (exiting)
        {
            return;
        }

        using var form = new SettingsForm(settings, apiKey, httpClient, firstRun);
        var result = form.ShowDialog();
        if (result != DialogResult.OK)
        {
            if (firstRun)
            {
                ExitApplication();
            }
            return;
        }

        var oldSettings = settings;
        try
        {
            hotkeyWindow.Register(form.ResultSettings.HotkeyModifiers, form.ResultSettings.HotkeyVirtualKey);
            settingsStore.Save(form.ResultSettings, form.ApiKey);
            StartupManager.SetEnabled(form.ResultSettings.StartWithWindows);
            settings = form.ResultSettings;
            apiKey = form.ApiKey;
            pauseItem.Text = "Pause shortcut";
            trayIcon.Text = $"AutoAnki — {HotkeyFormatter.Format(settings.HotkeyModifiers, settings.HotkeyVirtualKey)}";
            Notify("AutoAnki is ready", $"Select text and press {HotkeyFormatter.Format(settings.HotkeyModifiers, settings.HotkeyVirtualKey)}.", ToolTipIcon.Info);
            logger.Info("settings", "Settings saved.");
        }
        catch (Win32Exception ex)
        {
            settings = oldSettings;
            TryRegisterConfiguredHotkey(showError: false);
            MessageBox.Show(ex.Message, "Shortcut unavailable", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowSettings(firstRun);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            settings = oldSettings;
            TryRegisterConfiguredHotkey(showError: false);
            logger.Error("settings-save", ex);
            MessageBox.Show(ex.Message, "Could not save settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TogglePause()
    {
        try
        {
            if (hotkeyWindow.IsPaused)
            {
                hotkeyWindow.Resume();
                pauseItem.Text = "Pause shortcut";
                Notify("AutoAnki resumed", "The global shortcut is active.", ToolTipIcon.Info);
            }
            else
            {
                hotkeyWindow.Pause();
                pauseItem.Text = "Resume shortcut";
                Notify("AutoAnki paused", "The global shortcut is disabled.", ToolTipIcon.Info);
            }
        }
        catch (Win32Exception ex)
        {
            Notify("Shortcut unavailable", ex.Message, ToolTipIcon.Error);
        }
    }

    private void TryRegisterConfiguredHotkey(bool showError)
    {
        try
        {
            hotkeyWindow.Register(settings.HotkeyModifiers, settings.HotkeyVirtualKey);
            trayIcon.Text = $"AutoAnki — {HotkeyFormatter.Format(settings.HotkeyModifiers, settings.HotkeyVirtualKey)}";
        }
        catch (Win32Exception ex)
        {
            logger.Error("hotkey", ex);
            if (showError)
            {
                Notify("Shortcut unavailable", "Open Settings and choose another shortcut.", ToolTipIcon.Error);
            }
        }
    }

    private async Task WatchSettingsEventAsync()
    {
        var handles = new WaitHandle[] { settingsEvent, shutdown.Token.WaitHandle };
        while (!shutdown.IsCancellationRequested)
        {
            var signaled = await Task.Run(() => WaitHandle.WaitAny(handles), shutdown.Token).ConfigureAwait(false);
            if (signaled != 0 && !shutdown.IsCancellationRequested)
            {
                break;
            }

            if (!shutdown.IsCancellationRequested && !dispatcher.IsDisposed)
            {
                dispatcher.BeginInvoke(() => ShowSettings(firstRun: false));
            }
        }
    }

    private AnkiConnectClient CreateAnkiClient() => new(httpClient, settings.AnkiEndpoint);

    private void Notify(string title, string message, ToolTipIcon icon)
    {
        if (!exiting)
        {
            activeToast?.Close();
            var toast = new AppToastForm(title, message, icon);
            activeToast = toast;
            toast.FormClosed += (_, _) =>
            {
                if (ReferenceEquals(activeToast, toast))
                {
                    activeToast = null;
                }

                toast.Dispose();
            };
            toast.Show();
            trayIcon.ShowBalloonTip(4000, title, message, icon);
        }
    }

    private void ExitApplication()
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        shutdown.Cancel();
        trayIcon.Visible = false;
        logger.Info("lifecycle", "AutoAnki stopped.");
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            shutdown.Cancel();
            activeToast?.Close();
            trayIcon.Dispose();
            hotkeyWindow.Dispose();
            dispatcher.Dispose();
            httpClient.Dispose();
            shutdown.Dispose();
            logger.Dispose();
        }

        base.Dispose(disposing);
    }
}
