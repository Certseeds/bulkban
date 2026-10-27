namespace bulkban.Paths;

internal static class PathResolver
{
    /// <summary>--certs-dir 优先,其次 exe 目录,最后当前工作目录。</summary>
    internal static string ResolveCertsDir(string? option, string baseDir, string currentDir)
    {
        if (!string.IsNullOrWhiteSpace(option))
        {
            return EnsureDirectory(option, "指定的证书目录");
        }

        var fromBase = Path.Combine(baseDir, "certs");
        if (Directory.Exists(fromBase))
        {
            return fromBase;
        }

        var fromCurrent = Path.Combine(currentDir, "certs");
        if (Directory.Exists(fromCurrent))
        {
            return fromCurrent;
        }

        throw new DirectoryNotFoundException(
            $"找不到证书目录。已尝试 {fromBase} 和 {fromCurrent};请用 --certs-dir 指定。");
    }

    /// <summary>--output-dir 优先,否则用证书目录的父目录。</summary>
    internal static string ResolveOutputDir(string? option, string certsDir, string currentDir)
    {
        if (!string.IsNullOrWhiteSpace(option))
        {
            return Path.GetFullPath(option);
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(certsDir));
        return string.IsNullOrEmpty(parent) ? Path.GetFullPath(currentDir) : parent;
    }

    private static string EnsureDirectory(string path, string label)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException($"{label}不存在:{full}");
        }

        return full;
    }
}
