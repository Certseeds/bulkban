using System.Security.Cryptography;
using System.Text;

namespace bulkban.Core.Policies;

/// <summary>RFC 4122 UUIDv5。</summary>
internal static class UuidV5
{
    internal static Guid Create(Guid namespaceId, string name)
    {
        var namespaceBytes = new byte[16];
        namespaceId.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var buffer = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(buffer, 0);
        nameBytes.CopyTo(buffer, namespaceBytes.Length);

        // UUIDv5 规范指定用 SHA-1。这里只用于生成稳定标识,不涉及安全用途。
#pragma warning disable CA5350
        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(buffer, hash);
#pragma warning restore CA5350

        Span<byte> result = stackalloc byte[16];
        hash[..16].CopyTo(result);
        result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
        result[8] = (byte)((result[8] & 0x3F) | 0x80); // RFC 4122 variant

        return new Guid(result, bigEndian: true);
    }
}
