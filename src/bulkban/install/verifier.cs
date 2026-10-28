using System.Text.Json;
using bulkban.Store;
using bulkban.Wdac;

namespace bulkban.Install;

internal static class Verifier
{
    /// <summary>返回全部问题(结构 + hash);空列表表示通过。</summary>
    internal static IReadOnlyList<string> Verify(string outputDir)
    {
        var (manifest, error) = Load(outputDir);
        if (manifest is null)
        {
            return new[] { error! };
        }

        var errors = new List<string>();
        errors.AddRange(VerifyStructure(manifest, outputDir));
        errors.AddRange(VerifyHashes(manifest, outputDir));
        return errors;
    }

    /// <summary>
    /// 校验模式目录与 CIP 内容:清单声明的 mode 必须和 cips/&lt;mode&gt;/ 路径及
    /// CIP 头里的 Audit 位一致。这部分错误不能被 --force 跳过。
    /// </summary>
    internal static IReadOnlyList<string> VerifyStructure(Manifest manifest, string outputDir)
    {
        var errors = new List<string>();
        var mode = ParseMode(manifest.Mode);
        if (mode is null)
        {
            errors.Add($"manifest 的 mode 无效:{manifest.Mode}");
            return errors;
        }

        var prefix = mode.Value.CipDirectory() + "/";
        var xmlPrefix = mode.Value.XmlDirectory() + "/";

        foreach (var group in manifest.Groups)
        {
            var relative = group.Cip.Replace('\\', '/');
            if (!relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"CIP 不在 {prefix} 目录下:{group.Cip}");
                continue;
            }

            if (!group.Xml.Replace('\\', '/').StartsWith(xmlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"XML 不在 {xmlPrefix} 目录下:{group.Xml}");
            }

            var full = ResolveUnder(outputDir, group.Cip, errors);
            if (full is null)
            {
                continue;
            }

            if (!File.Exists(full))
            {
                errors.Add($"缺少文件:{group.Cip}");
                continue;
            }

            var actual = CipModeInspector.Detect(full);
            if (actual is null)
            {
                errors.Add($"无法识别 CIP 模式:{group.Cip}");
            }
            else if (actual != mode)
            {
                errors.Add($"CIP 模式不匹配:{group.Cip}(清单 {manifest.Mode},实际 {actual})");
            }
        }

        return errors;
    }

    internal static IReadOnlyList<string> VerifyHashes(Manifest manifest, string outputDir)
    {
        var errors = new List<string>();
        foreach (var group in manifest.Groups)
        {
            VerifyFile(outputDir, group.Xml, group.XmlSha256, errors);
            VerifyFile(outputDir, group.Cip, group.CipSha256, errors);
        }

        return errors;
    }

    internal static (Manifest? Manifest, string? Error) Load(string outputDir)
    {
        var manifestPath = Path.Combine(outputDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return (null, $"找不到 manifest.json:{manifestPath}");
        }

        try
        {
            var manifest = JsonSerializer.Deserialize(
                File.ReadAllText(manifestPath),
                JsonContext.Default.Manifest);
            return manifest is null
                ? (null, "manifest.json 无法解析")
                : (manifest, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return (null, $"manifest.json 无法解析:{ex.Message}");
        }
    }

    private static void VerifyFile(string outputDir, string relativePath, string expected, List<string> errors)
    {
        var full = ResolveUnder(outputDir, relativePath, errors);
        if (full is null)
        {
            return;
        }

        if (!File.Exists(full))
        {
            errors.Add($"缺少文件:{relativePath}");
            return;
        }

        var actual = Hashing.Sha256File(full);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"hash 不匹配:{relativePath}(期望 {expected},实际 {actual})");
        }
    }

    private static string? ResolveUnder(string root, string relativePath, List<string> errors)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var prefix = rootFull + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(rootFull, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"路径越界:{relativePath}");
            return null;
        }

        return full;
    }

    private static PolicyMode? ParseMode(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "audit" => PolicyMode.Audit,
            "enforce" => PolicyMode.Enforce,
            _ => null,
        };
}
