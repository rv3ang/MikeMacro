using System.ComponentModel;
using System.Runtime.InteropServices;
using MikeMacro.Core.Models;
using MikeMacro.Core.Playback;

namespace MikeMacro.Platform.Windows;

public sealed class WindowsInputBackend : IInputBackend
{
    private readonly HashSet<ushort> heldKeys = [];
    private readonly HashSet<MouseButton> heldButtons = [];
    private readonly object sync = new();

    public ValueTask SendKeyAsync(KeyAction action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = action.Key.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ResolveVirtualKey)
            .ToArray();

        if (keys.Length == 0)
        {
            throw new ArgumentException("A key action must contain a key.", nameof(action));
        }

        switch (action.Kind)
        {
            case KeyActionKind.Down:
                SendKeys(keys, keyUp: false);
                break;
            case KeyActionKind.Up:
                SendKeys(keys, keyUp: true);
                break;
            default:
                SendKeys(keys, keyUp: false);
                SendKeys(keys.Reverse().ToArray(), keyUp: true);
                break;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SendMouseButtonAsync(MouseButtonAction action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var flags = MouseButtonFlags(action.Button);

        switch (action.Kind)
        {
            case KeyActionKind.Down:
                SendMouse(flags.Down);
                lock (sync)
                {
                    heldButtons.Add(action.Button);
                }

                break;
            case KeyActionKind.Up:
                SendMouse(flags.Up);
                lock (sync)
                {
                    heldButtons.Remove(action.Button);
                }

                break;
            default:
                SendMouse(flags.Down);
                SendMouse(flags.Up);
                break;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MoveMouseAsync(MouseMoveAction action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (action.Relative)
        {
            SendMouse(new MouseInput(action.X, action.Y, 0, MouseEventFlags.Move));
        }
        else
        {
            var width = GetSystemMetrics(SystemMetric.ScreenWidth);
            var height = GetSystemMetrics(SystemMetric.ScreenHeight);
            if (width <= 1 || height <= 1)
            {
                throw new InvalidOperationException("Windows reported an invalid screen size.");
            }

            var x = Math.Clamp(action.X * 65_535 / (width - 1), 0, 65_535);
            var y = Math.Clamp(action.Y * 65_535 / (height - 1), 0, 65_535);
            SendMouse(new MouseInput(x, y, 0, MouseEventFlags.Move | MouseEventFlags.Absolute));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask TypeTextAsync(TextAction action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputs = new List<Input>(action.Text.Length * 2);
        foreach (var character in action.Text)
        {
            inputs.Add(Input.Keyboard(new KeyboardInput(0, character, KeyboardEventFlags.Unicode)));
            inputs.Add(Input.Keyboard(new KeyboardInput(0, character, KeyboardEventFlags.Unicode | KeyboardEventFlags.KeyUp)));
        }

        SendInputs(inputs);
        return ValueTask.CompletedTask;
    }

    public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ushort[] keys;
        MouseButton[] buttons;
        lock (sync)
        {
            keys = heldKeys.ToArray();
            buttons = heldButtons.ToArray();
            heldKeys.Clear();
            heldButtons.Clear();
        }

        if (keys.Length > 0)
        {
            SendKeys(keys, keyUp: true);
        }

        foreach (var button in buttons)
        {
            SendMouse(MouseButtonFlags(button).Up);
        }

        return ValueTask.CompletedTask;
    }

    private void SendKeys(IEnumerable<ushort> keys, bool keyUp)
    {
        var flags = keyUp ? KeyboardEventFlags.KeyUp : KeyboardEventFlags.None;
        var inputs = keys.Select(key => Input.Keyboard(new KeyboardInput(key, 0, flags))).ToList();
        SendInputs(inputs);
        lock (sync)
        {
            foreach (var key in keys)
            {
                if (keyUp)
                {
                    heldKeys.Remove(key);
                }
                else
                {
                    heldKeys.Add(key);
                }
            }
        }
    }

    private static void SendMouse(MouseInput mouse) => SendInputs([Input.Mouse(mouse)]);

    private static void SendInputs(IReadOnlyList<Input> inputs)
    {
        if (inputs.Count == 0)
        {
            return;
        }

        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        if (sent != inputs.Count)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not inject the complete input sequence.");
        }
    }

    private static ushort ResolveVirtualKey(string key)
    {
        key = key.Trim().ToUpperInvariant();
        if (key.Length == 1 && ((key[0] is >= 'A' and <= 'Z') || (key[0] is >= '0' and <= '9')))
        {
            return key[0];
        }

        if (key.StartsWith('F') && ushort.TryParse(key[1..], out var functionNumber) && functionNumber is >= 1 and <= 24)
        {
            return (ushort)(0x70 + functionNumber - 1);
        }

        return key switch
        {
            "SHIFT" => 0x10,
            "CTRL" or "CONTROL" => 0x11,
            "ALT" => 0x12,
            "WIN" or "WINDOWS" => 0x5B,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "BACKSPACE" => 0x08,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "DELETE" => 0x2E,
            "INSERT" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            _ => throw new ArgumentException($"Unsupported Windows key: {key}.", nameof(key))
        };
    }

    private static (MouseInput Down, MouseInput Up) MouseButtonFlags(MouseButton button) => button switch
    {
        MouseButton.Left => (new MouseInput(0, 0, 0, MouseEventFlags.LeftDown), new MouseInput(0, 0, 0, MouseEventFlags.LeftUp)),
        MouseButton.Right => (new MouseInput(0, 0, 0, MouseEventFlags.RightDown), new MouseInput(0, 0, 0, MouseEventFlags.RightUp)),
        MouseButton.Middle => (new MouseInput(0, 0, 0, MouseEventFlags.MiddleDown), new MouseInput(0, 0, 0, MouseEventFlags.MiddleUp)),
        MouseButton.Back => (new MouseInput(0, 0, 1, MouseEventFlags.XDown), new MouseInput(0, 0, 1, MouseEventFlags.XUp)),
        MouseButton.Forward => (new MouseInput(0, 0, 2, MouseEventFlags.XDown), new MouseInput(0, 0, 2, MouseEventFlags.XUp)),
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInput);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(SystemMetric index);

    private enum SystemMetric
    {
        ScreenWidth = 0,
        ScreenHeight = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputData Data;

        public static Input Keyboard(KeyboardInput keyboard) => new() { Type = 1, Data = new InputData { Keyboard = keyboard } };

        public static Input Mouse(MouseInput mouse) => new() { Type = 0, Data = new InputData { Mouse = mouse } };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput(int dx, int dy, uint mouseData, MouseEventFlags flags)
    {
        public int Dx = dx;
        public int Dy = dy;
        public uint MouseData = mouseData;
        public MouseEventFlags Flags = flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput(ushort virtualKey, ushort scanCode, KeyboardEventFlags flags)
    {
        public ushort VirtualKey = virtualKey;
        public ushort ScanCode = scanCode;
        public KeyboardEventFlags Flags = flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [Flags]
    private enum KeyboardEventFlags : uint
    {
        None = 0,
        KeyUp = 0x0002,
        Unicode = 0x0004
    }

    [Flags]
    private enum MouseEventFlags : uint
    {
        Move = 0x0001,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010,
        MiddleDown = 0x0020,
        MiddleUp = 0x0040,
        XDown = 0x0080,
        XUp = 0x0100,
        Absolute = 0x8000
    }
}