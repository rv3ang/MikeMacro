namespace MikeMacro.Core.Triggers;

public sealed record HotkeyTrigger(string Gesture, string MacroName)
{
    public string NormalizedGesture => Normalize(Gesture);

    public HotkeyTrigger Validate()
    {
        if (string.IsNullOrWhiteSpace(Gesture))
        {
            throw new ArgumentException("A hotkey gesture is required.", nameof(Gesture));
        }

        if (string.IsNullOrWhiteSpace(MacroName))
        {
            throw new ArgumentException("A macro name is required for a hotkey trigger.", nameof(MacroName));
        }

        return this;
    }

    public static string Normalize(string gesture)
    {
        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(part => ModifierOrder(part))
            .ThenBy(part => part, StringComparer.Ordinal)
            .ToArray();

        return string.Join('+', parts);
    }

    private static int ModifierOrder(string key) => key switch
    {
        "CTRL" or "CONTROL" => 0,
        "ALT" => 1,
        "SHIFT" => 2,
        "WIN" or "WINDOWS" => 3,
        _ => 4
    };
}