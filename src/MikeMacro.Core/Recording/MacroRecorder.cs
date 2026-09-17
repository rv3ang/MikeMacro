using MikeMacro.Core.Models;

namespace MikeMacro.Core.Recording;

public sealed class MacroRecorder
{
    private readonly object sync = new();
    private readonly List<RecordedAction> recordedActions = [];
    private long lastTimestamp;
    private bool recording;

    public bool IsRecording
    {
        get
        {
            lock (sync)
            {
                return recording;
            }
        }
    }

    public void Start()
    {
        lock (sync)
        {
            recordedActions.Clear();
            lastTimestamp = 0;
            recording = true;
        }
    }

    public void Record(MacroAction action, long timestampMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (timestampMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds), "Timestamp cannot be negative.");
        }

        lock (sync)
        {
            if (!recording)
            {
                throw new InvalidOperationException("Recording has not started.");
            }

            if (recordedActions.Count > 0 && timestampMilliseconds < lastTimestamp)
            {
                throw new ArgumentException("Recorded timestamps must be monotonic.", nameof(timestampMilliseconds));
            }

            recordedActions.Add(new RecordedAction(timestampMilliseconds, action));
            lastTimestamp = timestampMilliseconds;
        }
    }

    public Macro Stop(string name, int repeatCount = 1)
    {
        lock (sync)
        {
            if (!recording)
            {
                throw new InvalidOperationException("Recording has not started.");
            }

            recording = false;
            if (recordedActions.Count == 0)
            {
                throw new InvalidOperationException("Cannot create a macro without recorded actions.");
            }

            var actions = new List<MacroAction>(recordedActions.Count * 2);
            var previousTimestamp = recordedActions[0].TimestampMilliseconds;
            foreach (var recorded in recordedActions)
            {
                var delay = recorded.TimestampMilliseconds - previousTimestamp;
                if (delay > 0)
                {
                    if (delay > int.MaxValue)
                    {
                        throw new ArgumentOutOfRangeException(nameof(recordedActions), "Recording gap is too large.");
                    }

                    actions.Add(new DelayAction((int)delay));
                }

                actions.Add(recorded.Action);
                previousTimestamp = recorded.TimestampMilliseconds;
            }

            return new Macro(name, actions, repeatCount).Validate();
        }
    }

    private sealed record RecordedAction(long TimestampMilliseconds, MacroAction Action);
}