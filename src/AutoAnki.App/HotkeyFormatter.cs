namespace AutoAnki.App;

internal static class HotkeyFormatter
{
    public static string Format(int modifiers, int virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & (int)NativeMethods.ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & (int)NativeMethods.ModShift) != 0) parts.Add("Shift");
        if ((modifiers & (int)NativeMethods.ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & (int)NativeMethods.ModWin) != 0) parts.Add("Win");
        parts.Add(((Keys)virtualKey).ToString());
        return string.Join('+', parts);
    }
}
