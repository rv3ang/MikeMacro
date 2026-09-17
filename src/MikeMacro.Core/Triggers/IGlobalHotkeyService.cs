namespace MikeMacro.Core.Triggers;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    void Register(nint windowHandle, IReadOnlyCollection<HotkeyTrigger> triggers);

    void Unregister();

    bool HandleWindowMessage(uint message, nint wParam);
}

public sealed class HotkeyPressedEventArgs(string gesture) : EventArgs
{
    public string Gesture { get; } = gesture;
}