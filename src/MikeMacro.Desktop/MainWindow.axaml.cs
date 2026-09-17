using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MikeMacro.Core.Models;
using MikeMacro.Core.Playback;
using MikeMacro.Core.Triggers;
#if NET8_0_WINDOWS
using Avalonia.Platform;
using MikeMacro.Platform.Windows;
#endif

namespace MikeMacro.Desktop;

public partial class MainWindow : Window
{
    private readonly MacroExecutionCoordinator coordinator;
    private readonly MacroProfile profile;
#if NET8_0_WINDOWS
    private WindowsGlobalHotkeyService? globalHotkeys;
    private WindowsWindowMessageBridge? hotkeyBridge;
#endif
    private readonly Macro macro = new("Demo sequence", [
        new KeyAction("Ctrl+S"),
        new TextAction("MikeMacro preview")
    ]);

    public ObservableCollection<string> Actions { get; } = [];

    public string Status { get; private set; } = "Preview mode does not send real input.";

    public MainWindow()
    {
        InitializeComponent();
        Opened += WindowOpened;
        Closed += WindowClosed;
        profile = new MacroProfile("Default", [macro], [new HotkeyTrigger("CTRL+S", macro.Name)]);
        coordinator = new MacroExecutionCoordinator(new MacroPlayer(new PreviewInputBackend()));
        Actions.Add("Press Ctrl+S");
        Actions.Add("Type \"MikeMacro preview\"");
        DataContext = this;
    }

    private void WindowOpened(object? sender, EventArgs args)
    {
#if NET8_0_WINDOWS
        var platformHandle = TryGetPlatformHandle();
        if (platformHandle?.Handle is not nint handle || handle == nint.Zero)
        {
            Status = "Windows hotkeys unavailable: no native window handle.";
            DataContext = null;
            DataContext = this;
            return;
        }

        globalHotkeys = new WindowsGlobalHotkeyService();
        globalHotkeys.HotkeyPressed += HotkeyPressed;
        hotkeyBridge = WindowsWindowMessageBridge.Attach(handle, globalHotkeys);
        globalHotkeys.Register(handle, profile.Triggers);
#endif
    }

    private void WindowClosed(object? sender, EventArgs args)
    {
#if NET8_0_WINDOWS
        hotkeyBridge?.Dispose();
        globalHotkeys?.Dispose();
        hotkeyBridge = null;
        globalHotkeys = null;
#endif
    }

#if NET8_0_WINDOWS
    private void HotkeyPressed(object? sender, HotkeyPressedEventArgs args)
    {
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            Status = $"Hotkey {args.Gesture} triggered preview.";
            DataContext = null;
            DataContext = this;
            await coordinator.ExecuteAsync(profile, args.Gesture);
        });
    }
#endif

    private async void PreviewClick(object? sender, RoutedEventArgs args)
    {
        Status = "Previewing...";
        var result = await coordinator.ExecuteAsync(profile, "CTRL+S");
        Status = result.Succeeded
            ? "Preview complete. No real input was sent."
            : $"Preview ended with status: {result.Status}.";
        DataContext = null;
        DataContext = this;
    }

    private sealed class PreviewInputBackend : IInputBackend
    {
        public ValueTask SendKeyAsync(KeyAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SendMouseButtonAsync(MouseButtonAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask MoveMouseAsync(MouseMoveAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask TypeTextAsync(TextAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}