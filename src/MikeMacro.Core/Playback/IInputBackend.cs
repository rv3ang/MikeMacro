using MikeMacro.Core.Models;

namespace MikeMacro.Core.Playback;

public interface IInputBackend
{
    ValueTask SendKeyAsync(KeyAction action, CancellationToken cancellationToken = default);

    ValueTask SendMouseButtonAsync(MouseButtonAction action, CancellationToken cancellationToken = default);

    ValueTask MoveMouseAsync(MouseMoveAction action, CancellationToken cancellationToken = default);

    ValueTask TypeTextAsync(TextAction action, CancellationToken cancellationToken = default);
}