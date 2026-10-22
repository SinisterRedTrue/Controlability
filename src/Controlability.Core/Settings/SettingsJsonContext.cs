using System.Text.Json;
using System.Text.Json.Serialization;

namespace Controlability.Core.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Settings))]
public sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
