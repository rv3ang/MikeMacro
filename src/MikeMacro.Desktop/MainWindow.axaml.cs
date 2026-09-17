using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MikeMacro.Core.Models;
using MikeMacro.Core.Playback;
using MikeMacro.Core.Recording;
using MikeMacro.Core.Storage;
using MikeMacro.Core.Triggers;
#if NET8_0_WINDOWS
using Avalonia.Platform;
using MikeMacro.Platform.Windows;
#endif

namespace MikeMacro.Desktop;

public partial class MainWindow : Window
{
    private readonly MacroExecutionCoordinator coordinator;
    private readonly MacroProjectStore projectStore = new();
    private MacroProfile profile;
#if NET8_0_WINDOWS
    private WindowsGlobalHotkeyService? globalHotkeys;
    private WindowsWindowMessageBridge? hotkeyBridge;
    private MacroRecordingSession? recordingSession;
#endif
    private Macro macro = new("Demo sequence", [
        new KeyAction("Ctrl+S"),
        new TextAction("MikeMacro preview")
    ]);

    public ObservableCollection<string> Actions { get; } = [];

    public ObservableCollection<string> MacroNames { get; } = [];

    public string MacroName => macro.Name;

    public string ActionCountText => $"{macro.Actions.Count} actions";

    public string Status { get; private set; } = "Preview mode does not send real input.";

    public MainWindow()
    {
        InitializeComponent();
        Opened += WindowOpened;
        Closed += WindowClosed;
        profile = new MacroProfile("Default", [macro], [new HotkeyTrigger("CTRL+S", macro.Name)]);
        coordinator = new MacroExecutionCoordinator(new MacroPlayer(new PreviewInputBackend()));
    #if NET8_0_WINDOWS
        recordingSession = new MacroRecordingSession(new WindowsInputCapture(), new MacroRecorder());
        recordingSession.ActionCaptured += RecordingActionCaptured;
    #endif
        Actions.Add("Press Ctrl+S");
        Actions.Add("Type \"MikeMacro preview\"");
        MacroNames.Add(macro.Name);
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
        recordingSession?.Dispose();
        hotkeyBridge = null;
        globalHotkeys = null;
        recordingSession = null;
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

    private async void SaveClick(object? sender, RoutedEventArgs args)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save macro",
                SuggestedFileName = $"{profile.Name}.mkmacro",
                DefaultExtension = "mkmacro",
                FileTypeChoices = [MacroFileType]
            });
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await projectStore.SaveProfileAsync(profile, stream);
            Status = $"Saved profile {profile.Name}.";
        }
        catch (Exception error)
        {
            Status = $"Could not save macro: {error.Message}";
        }

        DataContext = null;
        DataContext = this;
    }

    private async void LoadClick(object? sender, RoutedEventArgs args)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load macro",
                AllowMultiple = false,
                FileTypeFilter = [MacroFileType]
            });
            if (files.Count == 0)
            {
                return;
            }

            await using var stream = await files[0].OpenReadAsync();
            var loaded = await projectStore.LoadProfileOrMacroAsync(stream);
            ApplyProfile(loaded);
            Status = $"Loaded profile {loaded.Name}.";
        }
        catch (Exception error)
        {
            Status = $"Could not load macro: {error.Message}";
        }

        DataContext = null;
        DataContext = this;
    }

    private void RecordClick(object? sender, RoutedEventArgs args)
    {
#if NET8_0_WINDOWS
        try
        {
            recordingSession?.Start();
            Actions.Clear();
            RecordButton.IsEnabled = false;
            StopRecordingButton.IsEnabled = true;
            Status = "Recording keyboard and mouse input...";
        }
        catch (Exception error)
        {
            Status = $"Recording unavailable: {error.Message}";
        }
#else
        Status = "Recording is currently available on Windows only.";
#endif
        DataContext = null;
        DataContext = this;
    }

    private void StopRecordingClick(object? sender, RoutedEventArgs args)
    {
#if NET8_0_WINDOWS
        try
        {
            var recorded = recordingSession?.Stop("Recorded macro");
            RecordButton.IsEnabled = true;
            StopRecordingButton.IsEnabled = false;
            if (recorded is not null)
            {
                ApplyMacro(recorded);
            }
            Status = recorded is null
                ? "No recording was active."
                : $"Recorded {recorded.Actions.Count} actions.";
        }
        catch (Exception error)
        {
            RecordButton.IsEnabled = true;
            StopRecordingButton.IsEnabled = false;
            Status = $"Could not finish recording: {error.Message}";
        }
#else
        Status = "Recording is currently available on Windows only.";
#endif
        DataContext = null;
        DataContext = this;
    }

    private void DeleteActionClick(object? sender, RoutedEventArgs args)
    {
        var index = ActionList.SelectedIndex;
        if (index < 0)
        {
            Status = "Select an action first.";
        }
        else if (macro.Actions.Count == 1)
        {
            Status = "A macro must contain at least one action.";
        }
        else
        {
            var actions = macro.Actions.ToList();
            actions.RemoveAt(index);
            ApplyActions(actions, index - 1);
            Status = "Action deleted.";
        }

        DataContext = null;
        DataContext = this;
    }

    private void MacroSelectedClick(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Content: string name })
        {
            var selected = profile.Macros.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                RenderMacro(selected);
                Status = $"Selected {selected.Name}.";
                DataContext = null;
                DataContext = this;
            }
        }
    }

    private void NewMacroClick(object? sender, RoutedEventArgs args)
    {
        var name = "New macro";
        var suffix = 2;
        while (profile.Macros.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"New macro {suffix++}";
        }

        var created = new Macro(name, [new DelayAction(100)]);
        profile = new MacroProfile(profile.Name, profile.Macros.Append(created).ToArray(), profile.Triggers);
        RenderMacro(created);
        Status = $"Created {name}.";
        DataContext = null;
        DataContext = this;
    }

    private async void AddActionClick(object? sender, RoutedEventArgs args)
    {
        var dialog = new ActionDialog();
        var action = await dialog.ShowDialog<MacroAction?>(this);
        if (action is not null)
        {
            var actions = macro.Actions.ToList();
            var selectedIndex = ActionList.SelectedIndex;
            var insertIndex = selectedIndex < 0 ? actions.Count : selectedIndex + 1;
            actions.Insert(insertIndex, action);
            ApplyActions(actions, insertIndex);
            Status = "Action added.";
            DataContext = null;
            DataContext = this;
        }
    }

    private void DuplicateActionClick(object? sender, RoutedEventArgs args)
    {
        var index = ActionList.SelectedIndex;
        if (index < 0)
        {
            Status = "Select an action first.";
        }
        else
        {
            var actions = macro.Actions.ToList();
            actions.Insert(index + 1, actions[index]);
            ApplyActions(actions, index + 1);
            Status = "Action duplicated.";
        }

        DataContext = null;
        DataContext = this;
    }

    private void MoveUpClick(object? sender, RoutedEventArgs args) => MoveAction(-1);

    private void MoveDownClick(object? sender, RoutedEventArgs args) => MoveAction(1);

    private void MoveAction(int direction)
    {
        var index = ActionList.SelectedIndex;
        var newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= macro.Actions.Count)
        {
            Status = "Select an action that can be moved.";
        }
        else
        {
            var actions = macro.Actions.ToList();
            (actions[index], actions[newIndex]) = (actions[newIndex], actions[index]);
            ApplyActions(actions, newIndex);
            Status = "Action order updated.";
        }

        DataContext = null;
        DataContext = this;
    }

#if NET8_0_WINDOWS
    private void RecordingActionCaptured(object? sender, CapturedInputEventArgs args)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            Actions.Add(Describe(args.Action));
            Status = $"Recording: {args.Action.GetType().Name}";
            DataContext = null;
            DataContext = this;
        });
    }
#endif

    private static string Describe(MacroAction action) => action switch
    {
        KeyAction key => $"Key {key.Kind}: {key.Key}",
        MouseButtonAction mouse => $"Mouse {mouse.Kind}: {mouse.Button}",
        MouseMoveAction move => $"Move mouse: {move.X}, {move.Y}",
        TextAction text => $"Type: {text.Text}",
        DelayAction delay => $"Wait: {delay.Milliseconds} ms",
        _ => action.GetType().Name
    };

    private void ApplyMacro(Macro loaded)
    {
        var activeName = macro.Name;
        var macros = profile.Macros
            .Where(item => !string.Equals(item.Name, activeName, StringComparison.OrdinalIgnoreCase))
            .Append(loaded)
            .ToArray();
        var triggers = profile.Triggers
            .Where(trigger => macros.Any(item => string.Equals(item.Name, trigger.MacroName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (!triggers.Any(trigger => string.Equals(trigger.MacroName, loaded.Name, StringComparison.OrdinalIgnoreCase)))
        {
            triggers.Add(new HotkeyTrigger("CTRL+S", loaded.Name));
        }

        macro = loaded;
        profile = new MacroProfile(profile.Name, macros, triggers);
        RenderMacro(loaded);
    }

    private void RenderMacro(Macro loaded)
    {
        macro = loaded;
        Actions.Clear();
        foreach (var action in loaded.Actions)
        {
            Actions.Add(Describe(action));
        }

        ActionList.SelectedIndex = -1;
        RefreshMacroNames();
        DataContext = null;
        DataContext = this;
    }

    private void ApplyProfile(MacroProfile loaded)
    {
        profile = loaded;
        RenderMacro(loaded.Macros[0]);
    }

    private void RefreshMacroNames()
    {
        MacroNames.Clear();
        foreach (var item in profile.Macros)
        {
            MacroNames.Add(item.Name);
        }
    }

    private void ApplyActions(IReadOnlyList<MacroAction> actions, int selectedIndex)
    {
        ApplyMacro(new Macro(macro.Name, actions, macro.RepeatCount));
        ActionList.SelectedIndex = selectedIndex;
    }

    private static readonly FilePickerFileType MacroFileType = new("MikeMacro file")
    {
        Patterns = ["*.mkmacro", "*.json"]
    };

    private sealed class PreviewInputBackend : IInputBackend
    {
        public ValueTask SendKeyAsync(KeyAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SendMouseButtonAsync(MouseButtonAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask MoveMouseAsync(MouseMoveAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask TypeTextAsync(TextAction action, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}