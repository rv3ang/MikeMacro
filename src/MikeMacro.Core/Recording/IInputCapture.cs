using MikeMacro.Core.Models;

namespace MikeMacro.Core.Recording;

public interface IInputCapture : IDisposable
{
    event EventHandler<CapturedInputEventArgs>? InputCaptured;

    bool IsCapturing { get; }

    void Start();

    void Stop();
}

public sealed class CapturedInputEventArgs(long timestampMilliseconds, MacroAction action) : EventArgs
{
    public long TimestampMilliseconds { get; } = timestampMilliseconds;

    public MacroAction Action { get; } = action;
}