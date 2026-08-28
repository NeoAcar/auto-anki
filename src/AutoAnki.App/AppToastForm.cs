using System.Media;

namespace AutoAnki.App;

internal sealed class AppToastForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly System.Windows.Forms.Timer closeTimer = new() { Interval = 6500 };
    private readonly ToolTipIcon icon;

    public AppToastForm(string title, string message, ToolTipIcon icon)
    {
        this.icon = icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(30, 30, 30);
        ClientSize = new Size(420, 112);
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(18, 14, 18, 14);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        var accent = new Panel
        {
            BackColor = AccentColor(icon),
            Dock = DockStyle.Left,
            Width = 6
        };
        var titleLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = Color.White,
            Height = 29,
            Text = title
        };
        var messageLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(225, 225, 225),
            Text = message
        };

        Controls.Add(messageLabel);
        Controls.Add(titleLabel);
        Controls.Add(accent);
        closeTimer.Tick += (_, _) => Close();
        Shown += (_, _) =>
        {
            PositionNearCursorScreen();
            PlaySound();
            closeTimer.Start();
        };
        Click += (_, _) => Close();
        titleLabel.Click += (_, _) => Close();
        messageLabel.Click += (_, _) => Close();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PositionNearCursorScreen()
    {
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            workingArea.Right - Width - 18,
            workingArea.Bottom - Height - 18);
    }

    private void PlaySound()
    {
        var sound = icon switch
        {
            ToolTipIcon.Error => SystemSounds.Hand,
            ToolTipIcon.Warning => SystemSounds.Exclamation,
            _ => SystemSounds.Asterisk
        };
        sound.Play();
    }

    private static Color AccentColor(ToolTipIcon icon) => icon switch
    {
        ToolTipIcon.Error => Color.FromArgb(232, 72, 85),
        ToolTipIcon.Warning => Color.FromArgb(245, 176, 65),
        _ => Color.FromArgb(64, 170, 110)
    };
}
