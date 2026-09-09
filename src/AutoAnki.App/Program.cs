namespace AutoAnki.App;

internal static class Program
{
    private const string MutexName = @"Local\AutoAnki.Singleton";
    private const string SettingsEventName = @"Local\AutoAnki.OpenSettings";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
            {
                return;
            }
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(SettingsEventName);
                existingEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first process is still starting; it will show its normal tray UI.
            }

            return;
        }

        using var settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEventName);
        using var context = new AutoAnkiApplicationContext(settingsEvent);
        Application.Run(context);
    }
}
