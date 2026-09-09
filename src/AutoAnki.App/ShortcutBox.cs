namespace AutoAnki.App;

internal sealed class ShortcutBox : TextBox
{
    public int Modifiers { get; private set; }
    public int VirtualKey { get; private set; }

    public ShortcutBox(int modifiers, int key)
    {
        ReadOnly = true;
        Width = 220;
        SetBinding(modifiers, key);
    }

    private void SetBinding(int modifiers, int key)
    {
        Modifiers = modifiers;
        VirtualKey = key;
        Text = HotkeyFormatter.Format(modifiers, key);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        var modifiers = (e.Control ? 2 : 0) | (e.Shift ? 4 : 0) | (e.Alt ? 1 : 0);
        if (modifiers != 0 && (e.KeyCode is >= Keys.F1 and <= Keys.F24 or >= Keys.A and <= Keys.Z or >= Keys.D0 and <= Keys.D9))
        {
            SetBinding(modifiers, (int)e.KeyCode);
        }
    }
}
