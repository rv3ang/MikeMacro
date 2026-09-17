using MikeMacro.Core.Models;

namespace MikeMacro.Core.Playback;

public sealed class MacroPlayer(IInputBackend input)
{
    public async ValueTask<PlaybackResult> PlayAsync(
        Macro macro,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        macro.Validate();
        var completedActions = 0;

        try
        {
            for (var repeat = 0; repeat < macro.RepeatCount; repeat++)
            {
                for (var actionIndex = 0; actionIndex < macro.Actions.Count; actionIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new PlaybackProgress(
                        macro.Name,
                        repeat + 1,
                        macro.RepeatCount,
                        actionIndex + 1,
                        macro.Actions.Count));
                    await ExecuteAsync(macro.Actions[actionIndex], cancellationToken);
                    completedActions++;
                }
            }

            return new PlaybackResult(PlaybackStatus.Completed, completedActions);
        }
        catch (OperationCanceledException cancellation) when (cancellationToken.IsCancellationRequested)
        {
            return new PlaybackResult(PlaybackStatus.Cancelled, completedActions, cancellation);
        }
        catch (Exception error)
        {
            return new PlaybackResult(PlaybackStatus.Failed, completedActions, error);
        }
        finally
        {
            await input.ReleaseAllAsync();
        }
    }

    private async ValueTask ExecuteAsync(MacroAction action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case KeyAction key:
                await input.SendKeyAsync(key, cancellationToken);
                break;
            case MouseButtonAction mouseButton:
                await input.SendMouseButtonAsync(mouseButton, cancellationToken);
                break;
            case MouseMoveAction mouseMove:
                await input.MoveMouseAsync(mouseMove, cancellationToken);
                break;
            case TextAction text:
                await input.TypeTextAsync(text, cancellationToken);
                break;
            case DelayAction delay:
                await Task.Delay(delay.Validate().Milliseconds, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported macro action: {action.GetType().Name}.");
        }
    }
}