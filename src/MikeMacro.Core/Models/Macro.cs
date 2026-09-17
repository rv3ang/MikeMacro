namespace MikeMacro.Core.Models;

public sealed record Macro(
    string Name,
    IReadOnlyList<MacroAction> Actions,
    int RepeatCount = 1)
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Macro Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported macro schema version: {SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("A macro name is required.", nameof(Name));
        }

        if (Actions.Count == 0)
        {
            throw new ArgumentException("A macro must contain at least one action.", nameof(Actions));
        }

        if (RepeatCount is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(RepeatCount), "Repeat count must be between 1 and 10,000.");
        }

        return this;
    }
}