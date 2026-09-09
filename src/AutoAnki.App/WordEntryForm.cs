using AutoAnki.Core;

namespace AutoAnki.App;

internal sealed class WordEntryForm : Form
{
    private readonly TextBox input = new() { Dock = DockStyle.Top, Font = new Font("Segoe UI", 14), Margin = new Padding(0, 8, 0, 8) };
    public string Term { get; private set; } = string.Empty;

    public WordEntryForm(string text, bool fromOcr)
    {
        Text = fromOcr ? "AutoAnki — OCR" : "AutoAnki — type a word";
        ClientSize = new Size(560, fromOcr ? 260 : 150);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16)
        };
        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = fromOcr
            ? "Check the text. Click a word below, or select/edit the phrase you want."
            : "Type an English word or short phrase."
        });
        input.Width = 525;
        input.Text = text;
        root.Controls.Add(input);
        if (fromOcr)
        {
            var words = new FlowLayoutPanel { Width = 525, Height = 100, AutoScroll = true };
            foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(100))
            {
                var button = new Button { Text = word, AutoSize = true };
                button.Click += (_, _) => { input.Text = word; input.Focus(); input.SelectAll(); };
                words.Controls.Add(button);
            }
            root.Controls.Add(words);
        }
        var add = new Button { Text = "Add — Enter", AutoSize = true };
        var cancel = new Button { Text = "Cancel — Esc", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Width = 525, Height = 40 };
        buttons.Controls.AddRange([add, cancel]);
        root.Controls.Add(buttons);
        Controls.Add(root);
        AcceptButton = add;
        CancelButton = cancel;
        add.Click += (_, _) =>
        {
            var result = SelectionNormalizer.Normalize(input.SelectionLength > 0 ? input.SelectedText : input.Text);
            if (!result.IsSuccess)
            {
                MessageBox.Show(this, "Choose 1–12 words, up to 120 characters.", "Check word");
                return;
            }
            Term = result.Text!;
            DialogResult = DialogResult.OK;
            Close();
        };
        Shown += (_, _) => { input.Focus(); input.SelectAll(); };
    }
}
