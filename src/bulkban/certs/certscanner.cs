namespace bulkban.Certs;

internal static class CertScanner
{
    internal const string OthersGroupName = "others";

    /// <summary>
    /// 单个 policy 的 signer 规则过多时,ConfigCI 的 ConvertFrom-CIPolicy 会抛
    /// System.OutOfMemoryException(实测 200 条可成功、more than 300 条失败)
    /// 这里按目录中的 .cer 数量提前设限,并要求用户拆分为多个子目录。
    /// </summary>
    internal const int MaxCerFilesPerGroup = 150;

    /// <summary>
    /// 每个直接子目录一组,根目录下的 .cer 与 others 子目录里的 .cer 合并为 others;
    /// 组内不递归。
    /// </summary>
    internal static IReadOnlyList<CertGroup> Scan(string certsDir)
    {
        if (!Directory.Exists(certsDir))
        {
            throw new InvalidOperationException($"证书目录不存在:{certsDir}");
        }

        var groups = new List<CertGroup>();
        string? othersDir = null;

        foreach (var dir in Directory.GetDirectories(certsDir))
        {
            var name = Path.GetFileName(dir);
            if (string.Equals(name, OthersGroupName, StringComparison.OrdinalIgnoreCase))
            {
                // others 目录不单独成组,内容并入顶层的 others 组。
                othersDir = dir;
                continue;
            }

            var files = CollectCerFiles(dir);
            if (files.Count > 0)
            {
                EnsureWithinLimit(name, files, certsDir);
                groups.Add(new CertGroup(name, files));
            }
        }

        var othersFiles = CollectCerFiles(certsDir);
        if (othersDir is not null)
        {
            othersFiles = othersFiles
                .Concat(CollectOthersDirectoryFiles(othersDir))
                .OrderBy(f => f.FileName, StringComparer.Ordinal)
                .ToList();
        }

        if (othersFiles.Count > 0)
        {
            EnsureWithinLimit(OthersGroupName, othersFiles, certsDir);
            groups.Add(new CertGroup(OthersGroupName, othersFiles));
        }

        if (groups.Count == 0)
        {
            throw new InvalidOperationException($"证书目录中没有找到 .cer 文件:{certsDir}");
        }

        groups.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return groups;
    }

    /// <summary>
    /// others 目录允许存在,但只能放 .cer;混入其他文件或子目录时直接报错,
    /// 避免把漏放的证书静默忽略。
    /// </summary>
    private static List<CertFile> CollectOthersDirectoryFiles(string dir)
    {
        var invalid = Directory.EnumerateFileSystemEntries(dir)
            .Where(entry => !IsCerFile(entry))
            .Select(entry => Path.GetFileName(entry))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (invalid.Count > 0)
        {
            throw new InvalidOperationException(
                $"证书目录 '{OthersGroupName}/' 中只能包含 .cer 文件,发现:{string.Join("、", invalid)}。");
        }

        return CollectCerFiles(dir);
    }

    private static List<CertFile> CollectCerFiles(string dir)
    {
        return Directory.EnumerateFiles(dir)
            .Where(IsCerFile)
            .Select(path => new CertFile(path, Path.GetFileName(path)))
            .OrderBy(f => f.FileName, StringComparer.Ordinal)
            .ToList();
    }

    private static void EnsureWithinLimit(string groupName, List<CertFile> files, string certsDir)
    {
        if (files.Count <= MaxCerFilesPerGroup)
        {
            return;
        }

        var othersNote = string.Equals(groupName, OthersGroupName, StringComparison.OrdinalIgnoreCase)
            ? "others 组按根目录与 others/ 合并计数,拆分时也要把根目录的 .cer 移入新子目录。"
            : string.Empty;

        throw new InvalidOperationException(
            $"证书组 '{groupName}' 包含 {files.Count} 个 .cer,超过单组上限 {MaxCerFilesPerGroup}(证书目录:{certsDir})。" +
            $"请把证书拆分到该目录下的多个子目录(每个子目录会生成独立 policy),例如 '{groupName}-2/';" +
            othersNote +
            "单个 policy 过大时 ConfigCI 的 ConvertFrom-CIPolicy 会内存溢出。");
    }

    private static bool IsCerFile(string path)
    {
        return !Directory.Exists(path)
            && string.Equals(Path.GetExtension(path), ".cer", StringComparison.OrdinalIgnoreCase);
    }
}
