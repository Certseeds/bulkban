namespace bulkban.Certs;

internal static class CertScanner
{
    internal const string OthersGroupName = "others";

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

    private static bool IsCerFile(string path)
    {
        return !Directory.Exists(path)
            && string.Equals(Path.GetExtension(path), ".cer", StringComparison.OrdinalIgnoreCase);
    }
}
