using System.Security.Cryptography;
using System.Text;

namespace bulkban.Core.Policies;

/// <summary>
/// 把构建产物整理成待写入的文件列表。
/// 目录结构固定为 &lt;mode&gt;-xmls / &lt;mode&gt;-cips,每个文件配一个 .sha256 sidecar。
/// </summary>
internal static class OutputLayout
{
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

    /// <summary>写 `<hex>  <文件名>` 形式的 sidecar,兼容 sha256sum -c。</summary>
    private static OutputFile Sidecar(string path, byte[] content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var name = path[(path.LastIndexOf('/') + 1)..];
        var line = $"{hash}  {name}\n";
        return new OutputFile(path + ".sha256", Encoding.UTF8.GetBytes(line));
    }
}
