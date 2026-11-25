using System.Text.Json;
using System.Text.Json.Serialization;
using bulkban.Core.Policies;

namespace bulkban.Store;

// Manifest 相关类型由 bulkban.core 提供,这里只序列化 exe 自己的 State。
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(State))]
[JsonSerializable(typeof(StateEntry))]
internal sealed partial class JsonContext : JsonSerializerContext
{
    internal static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
