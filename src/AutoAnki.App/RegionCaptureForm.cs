using System.Drawing.Imaging;

namespace AutoAnki.App;

internal sealed class RegionCaptureForm : Form
{
    private readonly Bitmap screenImage;
    private Point origin;
    private Rectangle selection;
    private bool dragging;

    public RegionCaptureForm()
    {
        var bounds = Screen.FromPoint(Cursor.Position).Bounds;
        screenImage = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(screenImage))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        Text = "AutoAnki — select OCR area";
    }

    public Bitmap Crop() => screenImage.Clone(selection, PixelFormat.Format32bppArgb);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(screenImage, 0, 0);
        using var shade = new SolidBrush(Color.FromArgb(95, Color.Black));
        e.Graphics.FillRectangle(shade, ClientRectangle);
        if (selection.Width > 0 && selection.Height > 0)
        {
            e.Graphics.DrawImage(screenImage, selection, selection, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.DeepSkyBlue, 2);
            e.Graphics.DrawRectangle(pen, selection);
        }
        TextRenderer.DrawText(e.Graphics, "Drag around the word or subtitle • Esc: cancel", Font,
            new Point(20, 20), Color.White, Color.FromArgb(30, 30, 30));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Close(); return; }
        if (e.Button != MouseButtons.Left) return;
        origin = e.Location;
        dragging = true;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!dragging) return;
        var end = new Point(Math.Clamp(e.X, 0, ClientSize.Width), Math.Clamp(e.Y, 0, ClientSize.Height));
        selection = Rectangle.FromLTRB(Math.Min(origin.X, end.X), Math.Min(origin.Y, end.Y),
            Math.Max(origin.X, end.X), Math.Max(origin.Y, end.Y));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging || e.Button != MouseButtons.Left) return;
        OnMouseMove(e);
        dragging = false;
        Capture = false;
        if (selection.Width < 5 || selection.Height < 5) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) screenImage.Dispose();
        base.Dispose(disposing);
    }
}
