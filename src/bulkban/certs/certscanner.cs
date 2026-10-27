namespace bulkban.Certs;

internal static class CertScanner
{
    internal const string OthersGroupName = "others";

    /// <summary>
    /// 每个直接子目录一组,根目录下的 .cer 归入 others;组内不递归。
    /// </summary>
    internal static IReadOnlyList<CertGroup> Scan(string certsDir)
    {
        if (!Directory.Exists(certsDir))
        {
            throw new InvalidOperationException($"证书目录不存在:{certsDir}");
        }

        var groups = new List<CertGroup>();

        foreach (var dir in Directory.GetDirectories(certsDir))
        {
            var name = Path.GetFileName(dir);
            if (string.Equals(name, OthersGroupName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"证书目录下存在名为 '{name}' 的子目录,与顶层证书组 {OthersGroupName} 冲突,请重命名。");
            }

            var files = CollectCerFiles(dir);
            if (files.Count > 0)
            {
                groups.Add(new CertGroup(name, files));
            }
        }

        var topLevel = CollectCerFiles(certsDir);
        if (topLevel.Count > 0)
        {
            groups.Add(new CertGroup(OthersGroupName, topLevel));
        }

        if (groups.Count == 0)
        {
            throw new InvalidOperationException($"证书目录中没有找到 .cer 文件:{certsDir}");
        }

        groups.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return groups;
    }

    private static List<CertFile> CollectCerFiles(string dir)
    {
        return Directory.GetFiles(dir, "*.cer")
            .Select(path => new CertFile(path, Path.GetFileName(path)))
            .OrderBy(f => f.FileName, StringComparer.Ordinal)
            .ToList();
    }
}
