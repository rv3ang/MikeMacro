using MikeMacro.Core.Models;

namespace MikeMacro.Core.Playback;

public sealed class MacroPlayer(IInputBackend input)
{
    public async ValueTask PlayAsync(Macro macro, CancellationToken cancellationToken = default)
    {
        macro.Validate();

        for (var repeat = 0; repeat < macro.RepeatCount; repeat++)
        {
            foreach (var action in macro.Actions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteAsync(action, cancellationToken);
            }
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