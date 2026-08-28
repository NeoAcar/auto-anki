namespace AutoAnki.App;

public sealed class FileLogger : IDisposable
{
    private readonly object gate = new();
    private readonly string logDirectory;
    private bool disposed;

    public FileLogger(string? directory = null)
    {
        logDirectory = directory ?? AppPaths.LogDirectory;
        Directory.CreateDirectory(logDirectory);
        CleanupOldLogs();
    }

    public void Info(string category, string message) => Write("INFO", category, message);

    public void Error(string category, Exception exception) =>
        Write("ERROR", category, $"{exception.GetType().Name}: {Sanitize(exception.Message)}");

    private void Write(string level, string category, string message)
    {
        if (disposed)
        {
            return;
        }

        var line = $"{DateTimeOffset.Now:O}\t{level}\t{Sanitize(category)}\t{Sanitize(message)}{Environment.NewLine}";
        var path = Path.Combine(logDirectory, $"autoanki-{DateTime.Now:yyyy-MM-dd}.log");
        lock (gate)
        {
            File.AppendAllText(path, line);
        }
    }

    private void CleanupOldLogs()
    {
        var threshold = DateTime.UtcNow.AddDays(-7);
        foreach (var path in Directory.EnumerateFiles(logDirectory, "autoanki-*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < threshold)
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Logging must never prevent app startup.
            }
        }
    }

    private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ');

    public void Dispose() => disposed = true;
}
