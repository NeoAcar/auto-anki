namespace AutoAnki.App;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoAnki");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
}
