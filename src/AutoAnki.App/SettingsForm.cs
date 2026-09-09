using AutoAnki.Core;

namespace AutoAnki.App;

internal sealed class SettingsForm : Form
{
    private readonly HttpClient httpClient;
    private readonly AppSettings originalSettings;
    private readonly bool firstRun;
    private readonly ShortcutBox ocrShortcut;
    private readonly ShortcutBox manualShortcut;
    private readonly CancellationTokenSource cancellation = new();
    private readonly TextBox apiKeyBox = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly ComboBox deckBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly Label ankiStatus = new() { AutoSize = true, Text = "Not checked" };
    private readonly Label geminiStatus = new() { AutoSize = true, Text = "Not checked" };
    private readonly CheckBox ctrlBox = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox shiftBox = new() { Text = "Shift", AutoSize = true };
    private readonly CheckBox altBox = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox winBox = new() { Text = "Win", AutoSize = true };
    private readonly ComboBox keyBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly CheckBox startupBox = new() { Text = "Start AutoAnki when I sign in to Windows", AutoSize = true };
    private readonly Button refreshDecksButton = new() { Text = "Refresh decks", AutoSize = true };
    private readonly Button testGeminiButton = new() { Text = "Test Gemini", AutoSize = true };
    private readonly Button saveButton = new() { Text = "Save", AutoSize = true };
    private readonly Button cancelButton = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };

    public SettingsForm(AppSettings settings, string apiKey, HttpClient httpClient, bool firstRun)
    {
        this.httpClient = httpClient;
        originalSettings = settings;
        this.firstRun = firstRun;
        ocrShortcut = new ShortcutBox(settings.OcrHotkeyModifiers, settings.OcrHotkeyVirtualKey);
        manualShortcut = new ShortcutBox(settings.ManualHotkeyModifiers, settings.ManualHotkeyVirtualKey);
        ResultSettings = settings;
        ApiKey = apiKey;

        Text = firstRun ? "Set up AutoAnki" : "AutoAnki Settings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(580, 430);
        Size = new Size(680, 650);
        ShowInTaskbar = true;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        apiKeyBox.Text = apiKey;
        deckBox.Text = settings.TargetDeck;
        startupBox.Checked = settings.StartWithWindows;
        ctrlBox.Checked = (settings.HotkeyModifiers & (int)NativeMethods.ModControl) != 0;
        shiftBox.Checked = (settings.HotkeyModifiers & (int)NativeMethods.ModShift) != 0;
        altBox.Checked = (settings.HotkeyModifiers & (int)NativeMethods.ModAlt) != 0;
        winBox.Checked = (settings.HotkeyModifiers & (int)NativeMethods.ModWin) != 0;

        PopulateKeyChoices(settings.HotkeyVirtualKey);
        BuildLayout();

        refreshDecksButton.Click += async (_, _) => await RefreshDecksAsync();
        testGeminiButton.Click += async (_, _) => await TestGeminiAsync();
        saveButton.Click += async (_, _) => await SaveAsync();
        FormClosed += (_, _) => cancellation.Cancel();
        Shown += async (_, _) => await RefreshDecksAsync();
    }

    public AppSettings ResultSettings { get; private set; }
    public string ApiKey { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 10,
            AutoSize = true
        };

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(580, 0),
            Text = "Keep Anki Desktop open while using AutoAnki. Select text in any application, then press the global shortcut to create a vocabulary card."
        };

        root.Controls.Add(intro);
        root.Controls.Add(CreateSection("Gemini API key", apiKeyBox, testGeminiButton, geminiStatus,
            "Create a free key in Google AI Studio. It is encrypted for your Windows account."));
        root.Controls.Add(CreateSection("Target Anki deck", deckBox, refreshDecksButton, ankiStatus,
            "Choose an existing deck or type a new deck name."));

        var hotkeyPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        hotkeyPanel.Controls.AddRange([ctrlBox, shiftBox, altBox, winBox, keyBox]);
        root.Controls.Add(CreateLabeledGroup("Global shortcut", hotkeyPanel));
        root.Controls.Add(CreateLabeledGroup("OCR area — click and press a shortcut", ocrShortcut));
        root.Controls.Add(CreateLabeledGroup("Type a word — click and press a shortcut", manualShortcut));
        root.Controls.Add(startupBox);

        var note = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(580, 0),
            Text = "Selected text is sent to MyMemory and Google Gemini. AnkiConnect remains local on 127.0.0.1."
        };
        root.Controls.Add(note);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        cancelButton.Text = firstRun ? "Exit" : "Cancel";
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        root.Controls.Add(buttons);

        Controls.Add(root);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private static Control CreateSection(string title, Control input, Control button, Label status, string help)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 8, 0, 8)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLabel = new Label { Text = title, AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold) };
        var helpLabel = new Label { Text = help, AutoSize = true, ForeColor = SystemColors.GrayText };
        table.Controls.Add(titleLabel, 0, 0);
        table.SetColumnSpan(titleLabel, 2);
        table.Controls.Add(input, 0, 1);
        table.Controls.Add(button, 1, 1);
        table.Controls.Add(helpLabel, 0, 2);
        table.SetColumnSpan(helpLabel, 2);
        table.Controls.Add(status, 0, 3);
        table.SetColumnSpan(status, 2);
        return table;
    }

    private static Control CreateLabeledGroup(string title, Control content)
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold) });
        panel.Controls.Add(content);
        return panel;
    }

    private void PopulateKeyChoices(int selectedVirtualKey)
    {
        var choices = new List<KeyChoice>();
        for (var key = Keys.F1; key <= Keys.F24; key++) choices.Add(new KeyChoice(key.ToString(), (int)key));
        for (var key = Keys.A; key <= Keys.Z; key++) choices.Add(new KeyChoice(key.ToString(), (int)key));
        for (var key = Keys.D0; key <= Keys.D9; key++) choices.Add(new KeyChoice(((int)key - (int)Keys.D0).ToString(), (int)key));

        keyBox.DataSource = choices;
        keyBox.DisplayMember = nameof(KeyChoice.Name);
        keyBox.ValueMember = nameof(KeyChoice.VirtualKey);
        var requestedIndex = choices.FindIndex(choice => choice.VirtualKey == selectedVirtualKey);
        var fallbackIndex = choices.FindIndex(choice => choice.VirtualKey == (int)Keys.F12);
        keyBox.SelectedIndex = requestedIndex >= 0 ? requestedIndex : fallbackIndex;
    }

    private async Task RefreshDecksAsync()
    {
        var typedDeck = deckBox.Text.Trim();
        SetBusy(true);
        ankiStatus.Text = "Connecting to Anki…";
        try
        {
            var client = new AnkiConnectClient(httpClient, originalSettings.AnkiEndpoint);
            if (!await client.IsAvailableAsync(cancellation.Token))
            {
                ankiStatus.Text = "Anki is unavailable. Open Anki Desktop and try again.";
                return;
            }

            var decks = await client.GetDeckNamesAsync(cancellation.Token);
            deckBox.BeginUpdate();
            deckBox.Items.Clear();
            deckBox.Items.AddRange(decks.Cast<object>().ToArray());
            deckBox.EndUpdate();
            deckBox.Text = typedDeck;
            ankiStatus.Text = $"Connected — {decks.Count} deck(s) found.";
        }
        catch (Exception ex) when (ex is AutoAnkiException or HttpRequestException)
        {
            ankiStatus.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task TestGeminiAsync()
    {
        var key = apiKeyBox.Text.Trim();
        if (key.Length == 0)
        {
            geminiStatus.Text = "Enter a Gemini API key first.";
            return;
        }

        SetBusy(true);
        geminiStatus.Text = "Testing Gemini…";
        try
        {
            var provider = new GeminiExampleProvider(
                httpClient,
                originalSettings.GeminiEndpoint,
                key,
                originalSettings.GeminiModel);
            var result = await provider.TestAsync(cancellation.Token);
            geminiStatus.Text = result.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SaveAsync()
    {
        var key = apiKeyBox.Text.Trim();
        var deck = deckBox.Text.Trim();
        var modifiers = GetModifiers();
        if (key.Length == 0 || deck.Length == 0 || modifiers == 0 || keyBox.SelectedItem is not KeyChoice keyChoice)
        {
            MessageBox.Show(this, "Enter an API key and deck, and choose at least one shortcut modifier.", "AutoAnki", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        try
        {
            var bindings = new[] { (modifiers, keyChoice.VirtualKey),
                (ocrShortcut.Modifiers, ocrShortcut.VirtualKey), (manualShortcut.Modifiers, manualShortcut.VirtualKey) };
            if (bindings.Distinct().Count() != 3)
                throw new AutoAnkiException("Choose a different shortcut for each input method.");
            var anki = new AnkiConnectClient(httpClient, originalSettings.AnkiEndpoint);
            if (!await anki.IsAvailableAsync(cancellation.Token))
            {
                throw new AnkiUnavailableException("Open Anki Desktop before saving settings.");
            }

            var decks = await anki.GetDeckNamesAsync(cancellation.Token);
            if (!decks.Contains(deck, StringComparer.Ordinal))
            {
                await anki.CreateDeckAsync(deck, cancellation.Token);
            }

            await anki.EnsureNoteTypeAsync(cancellation.Token);

            var gemini = new GeminiExampleProvider(
                httpClient,
                originalSettings.GeminiEndpoint,
                key,
                originalSettings.GeminiModel);
            var geminiTest = await gemini.TestAsync(cancellation.Token);
            if (!geminiTest.Success)
            {
                throw new ProviderException(geminiTest.Message);
            }

            ResultSettings = originalSettings with
            {
                TargetDeck = deck,
                HotkeyModifiers = modifiers,
                HotkeyVirtualKey = keyChoice.VirtualKey,
                OcrHotkeyModifiers = ocrShortcut.Modifiers,
                OcrHotkeyVirtualKey = ocrShortcut.VirtualKey,
                ManualHotkeyModifiers = manualShortcut.Modifiers,
                ManualHotkeyVirtualKey = manualShortcut.VirtualKey,
                StartWithWindows = startupBox.Checked
            };
            ApiKey = key;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is AutoAnkiException or HttpRequestException)
        {
            MessageBox.Show(this, ex.Message, "Could not save AutoAnki settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private int GetModifiers()
    {
        var value = 0;
        if (ctrlBox.Checked) value |= (int)NativeMethods.ModControl;
        if (shiftBox.Checked) value |= (int)NativeMethods.ModShift;
        if (altBox.Checked) value |= (int)NativeMethods.ModAlt;
        if (winBox.Checked) value |= (int)NativeMethods.ModWin;
        return value;
    }

    private void SetBusy(bool busy)
    {
        refreshDecksButton.Enabled = !busy;
        testGeminiButton.Enabled = !busy;
        saveButton.Enabled = !busy;
        UseWaitCursor = busy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record KeyChoice(string Name, int VirtualKey);
}
