using System.Runtime.InteropServices;

namespace MikeMacro.Platform.Windows;

public sealed class WindowsWindowMessageBridge : IDisposable
{
    private const int WindowProcedureIndex = -4;
    private const uint HotkeyMessage = 0x0312;
    private readonly nint windowHandle;
    private readonly WindowsGlobalHotkeyService hotkeys;
    private readonly WindowProcedure procedure;
    private readonly nint previousProcedure;
    private bool disposed;

    private WindowsWindowMessageBridge(nint windowHandle, WindowsGlobalHotkeyService hotkeys)
    {
        this.windowHandle = windowHandle;
        this.hotkeys = hotkeys;
        procedure = WindowProcedureHandler;
        previousProcedure = SetWindowLongPtr(windowHandle, WindowProcedureIndex, Marshal.GetFunctionPointerForDelegate(procedure));
        if (previousProcedure == nint.Zero)
        {
            throw new InvalidOperationException("Could not attach the Windows hotkey message bridge.");
        }
    }

    public static WindowsWindowMessageBridge Attach(nint windowHandle, WindowsGlobalHotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        if (windowHandle == nint.Zero)
        {
            throw new ArgumentException("A native window handle is required.", nameof(windowHandle));
        }

        return new WindowsWindowMessageBridge(windowHandle, hotkeys);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        SetWindowLongPtr(windowHandle, WindowProcedureIndex, previousProcedure);
        disposed = true;
    }

    private nint WindowProcedureHandler(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == HotkeyMessage && hotkeys.HandleWindowMessage(message, wParam))
        {
            return nint.Zero;
        }

        return CallWindowProc(previousProcedure, hwnd, message, wParam, lParam);
    }

    private delegate nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int index, nint newValue);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CallWindowProc(nint previousProcedure, nint hWnd, uint message, nint wParam, nint lParam);
}