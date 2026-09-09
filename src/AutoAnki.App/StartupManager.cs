using Microsoft.Win32;

namespace AutoAnki.App;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AutoAnki";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("Windows shortcut support is unavailable.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(ShortcutPath);
                try
                {
                    shortcut.TargetPath = Application.ExecutablePath;
                    shortcut.Arguments = "--startup";
                    shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                    shortcut.Description = "AutoAnki — start at sign-in";
                    shortcut.Save();
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "AutoAnki.lnk");
}
