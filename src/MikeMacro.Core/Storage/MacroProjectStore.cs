using System.Text.Json;
using System.Text.Json.Serialization;
using MikeMacro.Core.Models;
using MikeMacro.Core.Triggers;

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

    public async Task SaveProfileAsync(MacroProfile profile, Stream destination, CancellationToken cancellationToken = default)
    {
        profile.Validate();
        await JsonSerializer.SerializeAsync(destination, profile, JsonOptions, cancellationToken);
    }

    public async Task<MacroProfile> LoadProfileAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var profile = await JsonSerializer.DeserializeAsync<MacroProfile>(source, JsonOptions, cancellationToken)
            ?? throw new JsonException("The profile file is empty.");

        return profile.Validate();
    }

    public async Task<MacroProfile> LoadProfileOrMacroAsync(Stream source, CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        using var document = await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
        buffer.Position = 0;

        if (document.RootElement.TryGetProperty("macros", out _))
        {
            return await LoadProfileAsync(buffer, cancellationToken);
        }

        var macro = await LoadAsync(buffer, cancellationToken);
        return new MacroProfile("Default", [macro], [new Triggers.HotkeyTrigger("CTRL+S", macro.Name)]).Validate();
    }
}