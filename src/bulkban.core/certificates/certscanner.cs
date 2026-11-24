namespace bulkban.Core.Certificates;

/// <summary>
/// 分组规则:每个直接子目录一组;根目录下的 .cer 与 others/ 里的合并为 others;组内不递归。
/// 实现上与具体存储无关,磁盘目录与 zip 条目共用同一套规则。
/// </summary>
internal static class CertScanner
{
    internal const string OthersGroupName = "others";

    /// <summary>
    /// 单个 policy 的 signer 规则过多时,ConfigCI 的 ConvertFrom-CIPolicy 会抛
    /// System.OutOfMemoryException(实测 200 条可成功、300 条以上失败)。
    /// 这里按目录中的 .cer 数量提前设限。
    /// </summary>
    internal const int MaxCerFilesPerGroup = 150;

    /// <summary>
    /// <paramref name="relativePaths"/> 是相对于 certs/ 根的路径,统一用 '/' 分隔。
    /// </summary>
    internal static IReadOnlyList<CertGroup> Group(IEnumerable<string> relativePaths)
    {
        var all = relativePaths
            .Select(p => p.Replace('\\', '/').TrimStart('/'))
            .Where(p => p.Length > 0 && IsCer(p))
            .ToList();

        if (all.Count == 0)
        {
            throw new InvalidOperationException("证书目录中没有找到 .cer 文件。");
        }

        var topLevel = all.Where(IsAtRoot).Select(ToCertFile).ToList();
        var othersDir = all
            .Where(p => p.Count(c => c == '/') == 1 && p.StartsWith(OthersGroupName + "/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var groups = new List<CertGroup>();

        foreach (var directory in all
                     .Select(p => p.Split('/')[0])
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            if (string.Equals(directory, OthersGroupName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var files = all
                .Where(p => p.Count(c => c == '/') == 1 && p.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(ToCertFile)
                .ToList();

            if (files.Count == 0)
            {
                continue;
            }

            EnsureWithinLimit(directory, files);
            groups.Add(new CertGroup(directory, files));
        }

        var othersFiles = topLevel
            .Concat(othersDir.OrderBy(p => p, StringComparer.Ordinal).Select(ToCertFile))
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();

        if (othersFiles.Count > 0)
        {
            EnsureWithinLimit(OthersGroupName, othersFiles);
            groups.Add(new CertGroup(OthersGroupName, othersFiles));
        }

        if (groups.Count == 0)
        {
            throw new InvalidOperationException("证书目录中没有找到 .cer 文件。");
        }

        groups.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return groups;
    }

    private static bool IsAtRoot(string path) => path.All(c => c != '/');

    private static CertFile ToCertFile(string relativePath) =>
        new(relativePath, relativePath[(relativePath.LastIndexOf('/') + 1)..]);

    private static bool IsCer(string path) =>
        path.EndsWith(".cer", StringComparison.OrdinalIgnoreCase);

    private static void EnsureWithinLimit(string groupName, List<CertFile> files)
    {
        if (files.Count <= MaxCerFilesPerGroup)
        {
            return;
        }

        var othersNote = string.Equals(groupName, OthersGroupName, StringComparison.OrdinalIgnoreCase)
            ? "others 组按根目录与 others/ 合并计数,拆分时也要把根目录的 .cer 移入新子目录。"
            : string.Empty;

        throw new InvalidOperationException(
            $"证书组 '{groupName}' 包含 {files.Count} 个 .cer,超过单组上限 {MaxCerFilesPerGroup}。" +
            $"请把证书拆分到多个子目录(每个子目录会生成独立 policy),例如 '{groupName}-2/'。" +
            othersNote +
            "单个 policy 过大时 ConfigCI 的 ConvertFrom-CIPolicy 会内存溢出。");
    }
}
