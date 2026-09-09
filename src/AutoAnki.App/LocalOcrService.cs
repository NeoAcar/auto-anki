using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using AutoAnki.Core;

namespace AutoAnki.App;

public sealed class LocalOcrService
{
    private OcrEngine? engine;

    public async Task<string> ReadAsync(Bitmap image, CancellationToken cancellationToken)
    {
        engine ??= OcrEngine.AvailableRecognizerLanguages
            .Where(language => language.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            .Select(OcrEngine.TryCreateFromLanguage).FirstOrDefault();
        if (engine is null)
        {
            throw new AutoAnkiException("English OCR is not installed. In Windows Settings > Time & language > Language options, install English Basic typing, then restart AutoAnki.");
        }

        // Upscale small subtitle crops, but never exceed the engine's supported dimensions.
        var scale = Math.Min(image.Height < 160 ? 2.0 : 1.0,
            (double)(OcrEngine.MaxImageDimension - 32) / Math.Max(image.Width, image.Height));
        using var prepared = new Bitmap(Math.Max(1, (int)(image.Width * scale)) + 24,
            Math.Max(1, (int)(image.Height * scale)) + 24, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(prepared))
        {
            graphics.Clear(image.GetPixel(0, 0));
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, new Rectangle(12, 12, prepared.Width - 24, prepared.Height - 24));
        }
        using var encoded = new MemoryStream();
        prepared.Save(encoded, ImageFormat.Png);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(encoded.ToArray());
            await writer.StoreAsync().AsTask(cancellationToken);
            writer.DetachStream();
        }
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            .AsTask(cancellationToken);
        var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken);
        return string.Join(" ", result.Lines.Select(line => line.Text));
    }
}
