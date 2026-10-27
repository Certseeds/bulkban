using System.Text.Json;
using System.Text.Json.Serialization;

namespace bulkban.Store;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(ManifestGroup))]
[JsonSerializable(typeof(UnsupportedCert))]
[JsonSerializable(typeof(State))]
[JsonSerializable(typeof(StateEntry))]
internal sealed partial class JsonContext : JsonSerializerContext
{
    internal static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
