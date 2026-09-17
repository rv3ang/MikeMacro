using MikeMacro.Core.Models;

namespace MikeMacro.Core.Triggers;

public sealed record MacroProfile(
    string Name,
    IReadOnlyList<Macro> Macros,
    IReadOnlyList<HotkeyTrigger> Triggers)
{
    public MacroProfile Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("A profile name is required.", nameof(Name));
        }

        if (Macros.Count == 0)
        {
            throw new ArgumentException("A profile must contain at least one macro.", nameof(Macros));
        }

        foreach (var macro in Macros)
        {
            macro.Validate();
        }

        var macroNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var macro in Macros)
        {
            if (!macroNames.Add(macro.Name))
            {
                throw new ArgumentException($"Duplicate macro name: {macro.Name}.", nameof(Macros));
            }
        }

        var gestures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trigger in Triggers)
        {
            trigger.Validate();
            if (!macroNames.Contains(trigger.MacroName))
            {
                throw new ArgumentException($"Trigger references missing macro: {trigger.MacroName}.", nameof(Triggers));
            }

            if (!gestures.Add(trigger.NormalizedGesture))
            {
                throw new ArgumentException($"Duplicate hotkey gesture: {trigger.NormalizedGesture}.", nameof(Triggers));
            }
        }

        return this;
    }

    public Macro? Resolve(string gesture)
    {
        Validate();
        var normalized = HotkeyTrigger.Normalize(gesture);
        var trigger = Triggers.FirstOrDefault(item => item.NormalizedGesture == normalized);
        return trigger is null
            ? null
            : Macros.First(macro => string.Equals(macro.Name, trigger.MacroName, StringComparison.OrdinalIgnoreCase));
    }
}