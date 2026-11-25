using System.Text.Json;
using System.Text.Json.Serialization;

namespace bulkban.Core.Policies;

/// <summary>
/// 源生成的序列化上下文。core 要在 NativeAOT 下工作,不能依赖反射式序列化。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(ManifestGroup))]
[JsonSerializable(typeof(UnsupportedCert))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext
{
    internal static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
