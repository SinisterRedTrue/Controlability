using System.Text.Json;
using System.Text.Json.Serialization;

namespace Controlability.Core.Profiles;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Profile))]
public sealed partial class ProfilesJsonContext : JsonSerializerContext
{
}
