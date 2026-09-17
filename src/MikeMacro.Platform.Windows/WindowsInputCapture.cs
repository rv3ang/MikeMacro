using System.Diagnostics;
using System.Runtime.InteropServices;
using MikeMacro.Core.Models;
using MikeMacro.Core.Recording;

namespace MikeMacro.Platform.Windows;

public sealed class WindowsInputCapture : IInputCapture
{
    private const int KeyboardHook = 13;
    private const int MouseHook = 14;
    private const int KeyDownMessage = 0x0100;
    private const int KeyUpMessage = 0x0101;
    private const int SystemKeyDownMessage = 0x0104;
    private const int SystemKeyUpMessage = 0x0105;
    private const int MouseMoveMessage = 0x0200;
    private const int LeftDownMessage = 0x0201;
    private const int LeftUpMessage = 0x0202;
    private const int RightDownMessage = 0x0204;
    private const int RightUpMessage = 0x0205;
    private const int MiddleDownMessage = 0x0207;
    private const int MiddleUpMessage = 0x0208;
    private const int XDownMessage = 0x020B;
    private const int XUpMessage = 0x020C;
    private const uint InjectedFlag = 0x00000010;

    private readonly Stopwatch clock = new();
    private readonly HookCallback keyboardCallback;
    private readonly HookCallback mouseCallback;
    private nint keyboardHook;
    private nint mouseHook;
    private bool disposed;

    public WindowsInputCapture()
    {
        keyboardCallback = KeyboardHookCallback;
        mouseCallback = MouseHookCallback;
    }

    public event EventHandler<CapturedInputEventArgs>? InputCaptured;

    public bool IsCapturing => keyboardHook != nint.Zero || mouseHook != nint.Zero;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsCapturing)
        {
            throw new InvalidOperationException("Input capture is already active.");
        }

        clock.Restart();
        var module = GetModuleHandle(null);
        keyboardHook = SetWindowsHookEx(KeyboardHook, keyboardCallback, module, 0);
        mouseHook = SetWindowsHookEx(MouseHook, mouseCallback, module, 0);
        if (keyboardHook == nint.Zero || mouseHook == nint.Zero)
        {
            Stop();
            throw new InvalidOperationException("Windows could not install the input capture hooks.");
        }
    }

    public void Stop()
    {
        if (keyboardHook != nint.Zero)
        {
            UnhookWindowsHookEx(keyboardHook);
            keyboardHook = nint.Zero;
        }

        if (mouseHook != nint.Zero)
        {
            UnhookWindowsHookEx(mouseHook);
            mouseHook = nint.Zero;
        }

        clock.Stop();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Stop();
        disposed = true;
    }

    private nint KeyboardHookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<LowLevelKeyboardInput>(lParam);
            if ((data.Flags & InjectedFlag) == 0 && TryGetKeyName(data.VirtualKey, out var key))
            {
                var message = wParam.ToInt32();
                var kind = message is KeyDownMessage or SystemKeyDownMessage
                    ? KeyActionKind.Down
                    : KeyActionKind.Up;
                Publish(new KeyAction(key, kind));
            }
        }

        return CallNextHookEx(nint.Zero, code, wParam, lParam);
    }

    private nint MouseHookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<LowLevelMouseInput>(lParam);
            if ((data.Flags & InjectedFlag) == 0 && TryGetMouseAction(wParam.ToInt32(), data.MouseData, out var action))
            {
                Publish(action);
            }
            else if ((data.Flags & InjectedFlag) == 0 && wParam.ToInt32() == MouseMoveMessage)
            {
                Publish(new MouseMoveAction(data.Point.X, data.Point.Y));
            }
        }

        return CallNextHookEx(nint.Zero, code, wParam, lParam);
    }

    private void Publish(MacroAction action) =>
        InputCaptured?.Invoke(this, new CapturedInputEventArgs(clock.ElapsedMilliseconds, action));

    private static bool TryGetMouseAction(int message, uint mouseData, out MouseButtonAction action)
    {
        action = message switch
        {
            LeftDownMessage => new MouseButtonAction(MouseButton.Left, KeyActionKind.Down),
            LeftUpMessage => new MouseButtonAction(MouseButton.Left, KeyActionKind.Up),
            RightDownMessage => new MouseButtonAction(MouseButton.Right, KeyActionKind.Down),
            RightUpMessage => new MouseButtonAction(MouseButton.Right, KeyActionKind.Up),
            MiddleDownMessage => new MouseButtonAction(MouseButton.Middle, KeyActionKind.Down),
            MiddleUpMessage => new MouseButtonAction(MouseButton.Middle, KeyActionKind.Up),
            XDownMessage when (mouseData >> 16) == 1 => new MouseButtonAction(MouseButton.Back, KeyActionKind.Down),
            XUpMessage when (mouseData >> 16) == 1 => new MouseButtonAction(MouseButton.Back, KeyActionKind.Up),
            XDownMessage when (mouseData >> 16) == 2 => new MouseButtonAction(MouseButton.Forward, KeyActionKind.Down),
            XUpMessage when (mouseData >> 16) == 2 => new MouseButtonAction(MouseButton.Forward, KeyActionKind.Up),
            _ => null!
        };

        return action is not null;
    }

    private static bool TryGetKeyName(ushort virtualKey, out string key)
    {
        key = virtualKey switch
        {
            >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
            >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
            0x08 => "BACKSPACE",
            0x09 => "TAB",
            0x0D => "ENTER",
            0x10 or 0xA0 or 0xA1 => "SHIFT",
            0x11 or 0xA2 or 0xA3 => "CTRL",
            0x12 or 0xA4 or 0xA5 => "ALT",
            0x1B => "ESCAPE",
            0x20 => "SPACE",
            0x25 => "LEFT",
            0x26 => "UP",
            0x27 => "RIGHT",
            0x28 => "DOWN",
            0x2D => "INSERT",
            0x2E => "DELETE",
            _ => string.Empty
        };

        return key.Length > 0;
    }

    private delegate nint HookCallback(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelKeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelMouseInput
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookCallback callback, nint moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}