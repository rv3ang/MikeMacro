namespace MikeMacro.Core.Playback;

public enum PlaybackStatus
{
    Completed,
    Cancelled,
    Failed
}

public sealed record PlaybackProgress(
    string MacroName,
    int RepeatNumber,
    int RepeatCount,
    int ActionNumber,
    int ActionCount);

public sealed record PlaybackResult(
    PlaybackStatus Status,
    int CompletedActions,
    Exception? Error = null)
{
    public bool Succeeded => Status == PlaybackStatus.Completed;
}