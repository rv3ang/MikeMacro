using MikeMacro.Core.Playback;

namespace MikeMacro.Core.Triggers;

public sealed class MacroExecutionCoordinator(MacroPlayer player)
{
    private readonly object sync = new();
    private CancellationTokenSource? activeCancellation;

    public bool IsRunning
    {
        get
        {
            lock (sync)
            {
                return activeCancellation is not null;
            }
        }
    }

    public async Task<PlaybackResult> ExecuteAsync(
        MacroProfile profile,
        string gesture,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var macro = profile.Resolve(gesture)
            ?? throw new InvalidOperationException($"No macro is assigned to hotkey: {gesture}.");
        CancellationTokenSource linkedCancellation;
        lock (sync)
        {
            if (activeCancellation is not null)
            {
                throw new InvalidOperationException("A macro is already running.");
            }

            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            activeCancellation = linkedCancellation;
        }

        try
        {
            return await player.PlayAsync(macro, progress, linkedCancellation.Token);
        }
        finally
        {
            lock (sync)
            {
                activeCancellation = null;
            }

            linkedCancellation.Dispose();
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            activeCancellation?.Cancel();
        }
    }
}