using System.IO.Compression;

namespace bulkban.Web.Zips;

/// <summary>从上传的 zip 里读出 certs/ 下的所有条目路径与内容。</summary>
internal static class CertZipReader
{
    /// <summary>
    /// 返回 (certs 内的相对路径, 内容)。
    /// zip 里可能有 <c>xxx/certs/...</c> 这类前缀,按「最后一个 certs/ 段」定位。
    /// </summary>
    internal static IReadOnlyList<(string RelativePath, byte[] Content)> Read(Stream zipStream)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var entries = new List<(string, byte[])>();
        var prefix = FindCertsPrefix(archive);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue; // 目录条目
            }

            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = name[prefix.Length..];
            if (relative.Length == 0)
            {
                continue;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            entries.Add((relative, buffer.ToArray()));
        }

        return entries;
    }

    /// <summary>找 certs/ 前缀;找不到返回空串(视为整个 zip 就是 certs 目录)。</summary>
    private static string FindCertsPrefix(ZipArchive archive)
    {
        var candidates = archive.Entries
            .Select(e => e.FullName.Replace('\\', '/'))
            .Where(n => n.Contains("/certs/", StringComparison.OrdinalIgnoreCase)
                        || n.StartsWith("certs/", StringComparison.OrdinalIgnoreCase))
            .Select(n =>
            {
                var index = n.LastIndexOf("certs/", StringComparison.OrdinalIgnoreCase);
                return n[..(index + "certs/".Length)];
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p.Length)
            .ToList();

        return candidates.FirstOrDefault() ?? string.Empty;
    }
}
