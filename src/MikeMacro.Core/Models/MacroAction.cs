using System.Text.Json.Serialization;

namespace MikeMacro.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyAction), "key")]
[JsonDerivedType(typeof(MouseButtonAction), "mouse-button")]
[JsonDerivedType(typeof(MouseMoveAction), "mouse-move")]
[JsonDerivedType(typeof(TextAction), "text")]
[JsonDerivedType(typeof(DelayAction), "delay")]
public abstract record MacroAction;

public enum KeyActionKind
{
    Press,
    Down,
    Up
}

public enum MouseButton
{
    Left,
    Right,
    Middle,
    Back,
    Forward
}

public sealed record KeyAction(string Key, KeyActionKind Kind = KeyActionKind.Press) : MacroAction;

public sealed record MouseButtonAction(MouseButton Button, KeyActionKind Kind = KeyActionKind.Press) : MacroAction;

public sealed record MouseMoveAction(int X, int Y, bool Relative = false) : MacroAction;

public sealed record TextAction(string Text) : MacroAction;

public sealed record DelayAction(int Milliseconds) : MacroAction
{
    public DelayAction Validate()
    {
        if (Milliseconds is < 0 or > 86_400_000)
        {
            throw new ArgumentOutOfRangeException(nameof(Milliseconds), "Delay must be between 0 and 24 hours.");
        }

        return this;
    }
}