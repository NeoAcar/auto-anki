using AutoAnki.Core;
using System.Runtime.InteropServices;

namespace AutoAnki.App;

internal sealed class ClipboardCaptureService
{
    private static readonly string[] PreservedFormats =
    [
        DataFormats.UnicodeText,
        DataFormats.Text,
        DataFormats.Html,
        DataFormats.Rtf,
        DataFormats.Bitmap,
        DataFormats.FileDrop
    ];

    public async Task<SelectionCaptureResult> CaptureAsync(CancellationToken cancellationToken)
    {
        var snapshot = TrySnapshotClipboard();
        var sequenceBefore = NativeMethods.GetClipboardSequenceNumber();
        var clipboardChanged = false;

        try
        {
            for (var elapsed = 0; elapsed < 800 && NativeMethods.AreShortcutModifiersPressed(); elapsed += 25)
            {
                await Task.Delay(25, cancellationToken);
            }

            if (NativeMethods.AreShortcutModifiersPressed())
            {
                return SelectionCaptureResult.Failure(SelectionCaptureError.NoSelection);
            }

            NativeMethods.SendCtrlC();
            for (var elapsed = 0; elapsed < 1500; elapsed += 25)
            {
                await Task.Delay(25, cancellationToken);
                if (NativeMethods.GetClipboardSequenceNumber() != sequenceBefore)
                {
                    clipboardChanged = true;
                    break;
                }
            }

            if (!clipboardChanged)
            {
                return SelectionCaptureResult.Failure(SelectionCaptureError.NoSelection);
            }

            var copiedText = await ReadTextWithRetryAsync(cancellationToken);
            return SelectionNormalizer.Normalize(copiedText);
        }
        catch (ExternalException)
        {
            return SelectionCaptureResult.Failure(SelectionCaptureError.ClipboardUnavailable);
        }
        finally
        {
            if (clipboardChanged)
            {
                await RestoreClipboardAsync(snapshot);
            }
        }
    }

    private static DataObject? TrySnapshotClipboard()
    {
        try
        {
            var source = Clipboard.GetDataObject();
            if (source is null)
            {
                return null;
            }

            var snapshot = new DataObject();
            foreach (var format in PreservedFormats)
            {
                if (!source.GetDataPresent(format, autoConvert: false))
                {
                    continue;
                }

                var value = source.GetData(format, autoConvert: false);
                if (value is Image image)
                {
                    value = new Bitmap(image);
                }
                else if (value is string[] files)
                {
                    value = files.ToArray();
                }

                if (value is not null)
                {
                    snapshot.SetData(format, autoConvert: false, value);
                }
            }

            return snapshot.GetFormats(autoConvert: false).Length > 0 ? snapshot : null;
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static async Task<string?> ReadTextWithRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? Clipboard.GetText(TextDataFormat.UnicodeText)
                    : null;
            }
            catch (ExternalException) when (attempt < 9)
            {
                await Task.Delay(40, cancellationToken);
            }
        }

        return null;
    }

    private static async Task RestoreClipboardAsync(DataObject? snapshot)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (snapshot is null)
                {
                    Clipboard.Clear();
                }
                else
                {
                    Clipboard.SetDataObject(snapshot, copy: true, retryTimes: 5, retryDelay: 40);
                }

                return;
            }
            catch (ExternalException) when (attempt < 9)
            {
                await Task.Delay(40);
            }
        }
    }
}
