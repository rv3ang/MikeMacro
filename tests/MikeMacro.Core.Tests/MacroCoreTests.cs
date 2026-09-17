using MikeMacro.Core.Models;
using MikeMacro.Core.Playback;
using MikeMacro.Core.Storage;
using MikeMacro.Core.Triggers;
using Xunit;

namespace MikeMacro.Core.Tests;

public sealed class MacroCoreTests
{
    [Fact]
    public async Task ProjectStore_round_trips_all_action_types()
    {
        var original = new Macro("Demo", [
            new KeyAction("A", KeyActionKind.Down),
            new MouseButtonAction(MouseButton.Left),
            new MouseMoveAction(10, -5, Relative: true),
            new TextAction("hello"),
            new DelayAction(25)
        ]);
        await using var stream = new MemoryStream();
        var store = new MacroProjectStore();

        await store.SaveAsync(original, stream);
        stream.Position = 0;
        var restored = await store.LoadAsync(stream);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(Macro.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.RepeatCount, restored.RepeatCount);
        Assert.Equal(original.Actions, restored.Actions);
    }

    [Fact]
    public async Task Player_executes_actions_in_order_and_repeats()
    {
        var backend = new RecordingBackend();
        var macro = new Macro("Sequence", [new KeyAction("Enter"), new TextAction("ok")], RepeatCount: 2);

        var result = await new MacroPlayer(backend).PlayAsync(macro);

        Assert.True(result.Succeeded);
        Assert.Equal(["key:Enter:Press", "text:ok", "key:Enter:Press", "text:ok"], backend.Events);
        Assert.Equal(1, backend.ReleaseCount);
    }

    [Fact]
    public async Task Player_honors_cancellation_before_next_action()
    {
        var backend = new RecordingBackend();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await new MacroPlayer(backend).PlayAsync(
            new Macro("Cancelled", [new TextAction("never")]),
            cancellationToken: cancellation.Token);

        Assert.Equal(PlaybackStatus.Cancelled, result.Status);
        Assert.Empty(backend.Events);
        Assert.Equal(1, backend.ReleaseCount);
    }

    [Fact]
    public void Profile_normalizes_gestures_and_rejects_conflicts()
    {
        var macro = new Macro("Save", [new KeyAction("S")]);
        var profile = new MacroProfile("Default", [macro], [
            new HotkeyTrigger("shift + ctrl + s", "Save")
        ]);

        Assert.Equal("CTRL+SHIFT+S", profile.Resolve("CTRL+S+SHIFT") is not null
            ? profile.Triggers[0].NormalizedGesture
            : string.Empty);
        Assert.Throws<ArgumentException>(() => new MacroProfile("Default", [macro], [
            new HotkeyTrigger("CTRL+S", "Save"),
            new HotkeyTrigger("s + ctrl", "Save")
        ]).Validate());
    }

    [Fact]
    public async Task Coordinator_dispatches_hotkey_and_prevents_overlap()
    {
        var backend = new RecordingBackend();
        var coordinator = new MacroExecutionCoordinator(new MacroPlayer(backend));
        var profile = new MacroProfile("Default", [
            new Macro("Save", [new TextAction("saved")])
        ], [new HotkeyTrigger("CTRL+S", "Save")]);

        var result = await coordinator.ExecuteAsync(profile, "s + ctrl");

        Assert.True(result.Succeeded);
        Assert.Equal(["text:saved"], backend.Events);
        Assert.False(coordinator.IsRunning);
    }

    private sealed class RecordingBackend : IInputBackend
    {
        public List<string> Events { get; } = [];

        public int ReleaseCount { get; private set; }

        public ValueTask SendKeyAsync(KeyAction action, CancellationToken cancellationToken = default)
        {
            Events.Add($"key:{action.Key}:{action.Kind}");
            return ValueTask.CompletedTask;
        }

        public ValueTask SendMouseButtonAsync(MouseButtonAction action, CancellationToken cancellationToken = default)
        {
            Events.Add($"mouse:{action.Button}:{action.Kind}");
            return ValueTask.CompletedTask;
        }

        public ValueTask MoveMouseAsync(MouseMoveAction action, CancellationToken cancellationToken = default)
        {
            Events.Add($"move:{action.X},{action.Y}:{action.Relative}");
            return ValueTask.CompletedTask;
        }

        public ValueTask TypeTextAsync(TextAction action, CancellationToken cancellationToken = default)
        {
            Events.Add($"text:{action.Text}");
            return ValueTask.CompletedTask;
        }

        public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default)
        {
            ReleaseCount++;
            return ValueTask.CompletedTask;
        }
    }
}