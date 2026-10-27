using System.Security.Cryptography;
using System.Text;

namespace bulkban.Wdac;

/// <summary>RFC 4122 name-based UUID(v5),使用 SHA-1。</summary>
internal static class UuidV5
{
    internal static Guid Create(Guid namespaceId, string name)
    {
        var namespaceBytes = new byte[16];
        namespaceId.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var data = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(data, 0);
        nameBytes.CopyTo(data, namespaceBytes.Length);

        // RFC 4122 要求 v5 使用 SHA-1;这里只用于稳定 ID 派生,不用于安全场景。
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
        var hash = SHA1.HashData(data);
#pragma warning restore CA5350

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // RFC 4122 variant

        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
