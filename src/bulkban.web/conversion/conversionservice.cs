using bulkban.Core.Policies;
using bulkban.Web.Cip;
using bulkban.Web.Zips;

namespace bulkban.Web.Conversion;

/// <summary>一次转换的结果:产物 zip 的字节 + 人类可读的摘要。</summary>
internal sealed record ConversionResult(
    byte[] Zip,
    IReadOnlyList<string> Lines,
    IReadOnlyList<string> Warnings);

/// <summary>
/// 与 UI 无关的转换编排:读 zip → 两遍构建(audit + enforce) → 打包成一个 zip。
/// 两边都跑,是因为产物要求同时给出两套策略,便于先 audit 观察再切 enforce。
/// </summary>
internal static class ConversionService
{
    internal static ConversionResult Convert(Stream certZip)
    {
        var entries = CertZipReader.Read(certZip);
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("zip 里没有找到 certs 目录或其下的 .cer 文件。");
        }

        var byPath = entries
            .GroupBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Content, StringComparer.OrdinalIgnoreCase);

        byte[] Read(string relativePath) =>
            byPath.TryGetValue(relativePath, out var content)
                ? content
                : throw new FileNotFoundException($"zip 里找不到 {relativePath}");

        var warnings = new List<string>();
        var lines = new List<string>();
        var files = new List<OutputFile>();

        foreach (var mode in new[] { PolicyMode.Audit, PolicyMode.Enforce })
        {
            var artifacts = PolicyPipeline.Build(
                byPath.Keys,
                Read,
                policyXml => CodeIntegrityConverter.Convert(policyXml),
                mode,
                out var modeWarnings);

            warnings.AddRange(modeWarnings);
            files.AddRange(OutputLayout.Arrange(artifacts, mode));

            lines.Add($"[{mode.ToManifestValue()}] {artifacts.Count} 个 policy,"
                      + $"{artifacts.Sum(a => a.CertCount)} 张证书");
        }

        var zip = ResultZipWriter.Write(
            files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
            DateTimeOffset.UtcNow);

        return new ConversionResult(zip, lines, warnings);
    }
}
