using System.Security.Cryptography;

namespace bulkban.Store;

internal static class Hashing
{
    internal static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>写 `<hex>  <文件名>` 形式的 sidecar,兼容 sha256sum -c。</summary>
    internal static void WriteSidecar(string path)
    {
        var hash = Sha256File(path);
        var line = $"{hash}  {Path.GetFileName(path)}{Environment.NewLine}";
        File.WriteAllText(path + ".sha256", line);
    }
}
