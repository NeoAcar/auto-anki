using System.ComponentModel;

namespace AutoAnki.App;

internal sealed class GlobalHotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 0xA11;
    private bool registered;
    private int modifiers;
    private int virtualKey;

    public GlobalHotkeyWindow()
    {
        CreateHandle(new CreateParams { Caption = "AutoAnki Hotkey Window" });
    }

    public event EventHandler? Pressed;

    public bool IsPaused { get; private set; }

    public void Register(int newModifiers, int newVirtualKey)
    {
        Unregister();
        if (!NativeMethods.RegisterHotKey(
                Handle,
                HotkeyId,
                (uint)newModifiers | NativeMethods.ModNoRepeat,
                (uint)newVirtualKey))
        {
            throw new Win32Exception("That shortcut is already in use by another application.");
        }

        modifiers = newModifiers;
        virtualKey = newVirtualKey;
        registered = true;
        IsPaused = false;
    }

    public void Pause()
    {
        Unregister();
        IsPaused = true;
    }

    public void Resume()
    {
        if (modifiers == 0 || virtualKey == 0)
        {
            throw new InvalidOperationException("No shortcut has been configured.");
        }

        Register(modifiers, virtualKey);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmHotkey && message.WParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref message);
    }

    private void Unregister()
    {
        if (registered)
        {
            _ = NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }
}
