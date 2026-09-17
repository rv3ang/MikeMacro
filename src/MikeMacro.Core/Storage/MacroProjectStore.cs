using System.Text.Json;
using System.Text.Json.Serialization;
using MikeMacro.Core.Models;

namespace MikeMacro.Core.Storage;

public sealed class MacroProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task SaveAsync(Macro macro, Stream destination, CancellationToken cancellationToken = default)
    {
        macro.Validate();
        await JsonSerializer.SerializeAsync(destination, macro, JsonOptions, cancellationToken);
    }

    public async Task<Macro> LoadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var macro = await JsonSerializer.DeserializeAsync<Macro>(source, JsonOptions, cancellationToken)
            ?? throw new JsonException("The macro file is empty.");

        return macro.Validate();
    }
}