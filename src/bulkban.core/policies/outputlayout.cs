using System.Security.Cryptography;
using System.Text;

namespace bulkban.Core.Policies;

/// <summary>
/// 把构建产物整理成待写入的文件列表。
/// 目录结构固定为 &lt;mode&gt;-xmls / &lt;mode&gt;-cips,每个文件配一个 .sha256 sidecar,
/// 外加一份该模式的 manifest 与一个空的 manifest.json 占位文件。
/// </summary>
internal static class OutputLayout
{
    /// <summary>exe 的 install/verify 读这个固定名字;两种模式并存时由用户指定用哪份。</summary>
    internal const string PlaceholderManifestName = "manifest.json";

    internal static IReadOnlyList<OutputFile> Arrange(
        IReadOnlyList<PolicyArtifact> artifacts,
        PolicyMode mode)
    {
        var files = new List<OutputFile>();
        var xmlDir = mode.XmlDirectory();
        var cipDir = mode.CipDirectory();

        foreach (var artifact in artifacts.OrderBy(a => a.GroupName, StringComparer.Ordinal))
        {
            var xmlPath = $"{xmlDir}/{artifact.FileStem}.xml";
            var cipPath = $"{cipDir}/{artifact.FileStem}.cip";

            var xmlBytes = Encoding.UTF8.GetBytes(artifact.Xml);
            files.Add(new OutputFile(xmlPath, xmlBytes));
            files.Add(Sidecar(xmlPath, xmlBytes));
            files.Add(new OutputFile(cipPath, artifact.Cip));
            files.Add(Sidecar(cipPath, artifact.Cip));
        }

        return files;
    }

    /// <summary>
    /// 写一个模式的 manifest。文件名带模式前缀(audit-manifest.json),
    /// 因为同一个产物包里同时有 audit 与 enforce,不能都叫 manifest.json。
    /// </summary>
    internal static OutputFile ManifestFile(
        IReadOnlyList<PolicyArtifact> artifacts,
        PolicyMode mode,
        string certsDir,
        string toolVersion,
        DateTimeOffset generatedAt)
    {
        var groups = artifacts
            .OrderBy(a => a.GroupName, StringComparer.Ordinal)
            .Select(a => new ManifestGroup(
                Name: a.GroupName,
                PolicyId: "{" + a.PolicyId.ToString().ToUpperInvariant() + "}",
                CertCount: a.CertCount,
                Unsupported: a.Unsupported
                    .Select(u => new UnsupportedCert(u.FileName, u.Subject, u.UnsupportedReason ?? string.Empty))
                    .ToList(),
                Xml: $"{mode.XmlDirectory()}/{a.FileStem}.xml",
                XmlSha256: Sha256Hex(Encoding.UTF8.GetBytes(a.Xml)),
                Cip: $"{mode.CipDirectory()}/{a.FileStem}.cip",
                CipSha256: Sha256Hex(a.Cip)))
            .ToList();

        var manifest = new Manifest(
            SchemaVersion: 1,
            ToolVersion: toolVersion,
            GeneratedAt: generatedAt.ToString("O"),
            CertsDir: certsDir,
            Mode: mode.ToManifestValue(),
            Groups: groups);

        var json = System.Text.Json.JsonSerializer.Serialize(
            manifest, ManifestJsonContext.Default.Manifest);
        return new OutputFile(mode.ToManifestValue() + "-manifest.json", Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// 根目录的 manifest.json 只是占位:一个包里同时有 audit 与 enforce,
    /// 没法用单个文件表达,所以这里写一份解释性的空清单。
    /// </summary>
    internal static OutputFile PlaceholderManifest(PolicyMode[] modes) =>
        new(PlaceholderManifestName, Encoding.UTF8.GetBytes(
            "{\n" +
            "  \"note\": \"This package contains both audit and enforce policies.\",\n" +
            "  \"manifests\": [" +
            string.Join(", ", modes.Select(m => "\"" + m.ToManifestValue() + "-manifest.json\"")) +
            "]\n" +
            "}\n"));

    private static string Sha256Hex(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    /// <summary>写 `<hex>  <文件名>` 形式的 sidecar,兼容 sha256sum -c。</summary>
    private static OutputFile Sidecar(string path, byte[] content)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var line = $"{Sha256Hex(content)}  {name}\n";
        return new OutputFile(path + ".sha256", Encoding.UTF8.GetBytes(line));
    }
}
