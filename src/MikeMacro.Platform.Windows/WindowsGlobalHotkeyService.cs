using System.ComponentModel;
using System.Runtime.InteropServices;
using MikeMacro.Core.Triggers;

namespace MikeMacro.Platform.Windows;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const uint HotkeyMessage = 0x0312;
    private const uint NoRepeat = 0x4000;
    private readonly Dictionary<int, string> registrations = [];
    private nint windowHandle;
    private bool disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Register(nint windowHandle, IReadOnlyCollection<HotkeyTrigger> triggers)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (windowHandle == nint.Zero)
        {
            throw new ArgumentException("A native window handle is required.", nameof(windowHandle));
        }

        Unregister();
        this.windowHandle = windowHandle;
        var id = 1;
        try
        {
            foreach (var trigger in triggers)
            {
                trigger.Validate();
                var gesture = HotkeyTrigger.Normalize(trigger.Gesture);
                var parsed = ParseGesture(gesture);
                if (!RegisterHotKey(windowHandle, id, parsed.Modifiers | NoRepeat, parsed.VirtualKey))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register global hotkey: {gesture}.");
                }

                registrations.Add(id, gesture);
                id++;
            }
        }
        catch
        {
            Unregister();
            throw;
        }
    }

    public void Unregister()
    {
        if (windowHandle == nint.Zero)
        {
            registrations.Clear();
            return;
        }

        foreach (var id in registrations.Keys.ToArray())
        {
            UnregisterHotKey(windowHandle, id);
        }

        registrations.Clear();
        windowHandle = nint.Zero;
    }

    public bool HandleWindowMessage(uint message, nint wParam)
    {
        if (message != HotkeyMessage || !registrations.TryGetValue(wParam.ToInt32(), out var gesture))
        {
            return false;
        }

        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(gesture));
        return true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Unregister();
        disposed = true;
    }

    private static (uint Modifiers, uint VirtualKey) ParseGesture(string gesture)
    {
        var keys = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keys.Length == 0)
        {
            throw new ArgumentException("A hotkey gesture must contain at least one key.", nameof(gesture));
        }

        uint modifiers = 0;
        uint? virtualKey = null;
        foreach (var key in keys)
        {
            switch (key)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= 0x0002;
                    break;
                case "ALT":
                    modifiers |= 0x0001;
                    break;
                case "SHIFT":
                    modifiers |= 0x0004;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= 0x0008;
                    break;
                default:
                    if (virtualKey is not null)
                    {
                        throw new ArgumentException($"A hotkey can contain only one non-modifier key: {gesture}.", nameof(gesture));
                    }

                    virtualKey = ResolveVirtualKey(key);
                    break;
            }
        }

        return (modifiers, virtualKey ?? throw new ArgumentException($"A hotkey needs a non-modifier key: {gesture}.", nameof(gesture)));
    }

    private static uint ResolveVirtualKey(string key)
    {
        if (key.Length == 1 && ((key[0] is >= 'A' and <= 'Z') || (key[0] is >= '0' and <= '9')))
        {
            return key[0];
        }

        if (key.StartsWith('F') && uint.TryParse(key[1..], out var functionNumber) && functionNumber is >= 1 and <= 24)
        {
            return 0x70u + functionNumber - 1;
        }

        return key switch
        {
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "DELETE" => 0x2E,
            "INSERT" => 0x2D,
            _ => throw new ArgumentException($"Unsupported Windows hotkey: {key}.", nameof(key))
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}