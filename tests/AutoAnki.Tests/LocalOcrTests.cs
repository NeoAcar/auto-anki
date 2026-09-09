using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using AutoAnki.App;
using Xunit.Abstractions;

namespace AutoAnki.Tests;

public sealed class LocalOcrTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("This is a remarkable opportunity", 24, true)]
    [InlineData("perspicacious", 16, false)]
    [InlineData("The problem is now tractable", 20, true)]
    public async Task ReadsEnglishSubtitleLocally(string text, int size, bool dark)
    {
        using var image = new Bitmap(800, 70);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(dark ? Color.FromArgb(35, 35, 35) : Color.White);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            using var family = new FontFamily("Arial");
            path.AddString(text, family, (int)FontStyle.Regular, size, new Point(12, 15), StringFormat.GenericDefault);
            if (dark)
            {
                using var stroke = new Pen(Color.Black, 3);
                graphics.DrawPath(stroke, path);
            }
            graphics.FillPath(dark ? Brushes.White : Brushes.Black, path);
        }
        var timer = Stopwatch.StartNew();
        var result = await new LocalOcrService().ReadAsync(image, CancellationToken.None);
        output.WriteLine($"OCR {timer.ElapsedMilliseconds} ms: {result}");
        Assert.Equal(text.ToLowerInvariant(), result.ToLowerInvariant());
    }
}
