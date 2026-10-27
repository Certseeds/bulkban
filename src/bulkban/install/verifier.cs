using System.Text.Json;
using bulkban.Store;

namespace bulkban.Install;

internal static class Verifier
{
    /// <summary>返回与 manifest 记录不符的文件说明;空列表表示全部通过。</summary>
    internal static IReadOnlyList<string> Verify(string outputDir)
    {
        var errors = new List<string>();
        var manifestPath = Path.Combine(outputDir, "manifest.json");

        if (!File.Exists(manifestPath))
        {
            errors.Add($"找不到 manifest.json:{manifestPath}");
            return errors;
        }

        var manifest = JsonSerializer.Deserialize(
            File.ReadAllText(manifestPath),
            JsonContext.Default.Manifest);

        if (manifest is null)
        {
            errors.Add("manifest.json 无法解析");
            return errors;
        }

        foreach (var group in manifest.Groups)
        {
            VerifyFile(outputDir, group.Xml, group.XmlSha256, errors);
            VerifyFile(outputDir, group.Cip, group.CipSha256, errors);
        }

        return errors;
    }

    private static void VerifyFile(string outputDir, string relativePath, string expected, List<string> errors)
    {
        var full = Path.Combine(outputDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
}
