using bulkban.Core.Certificates;

namespace bulkban.Core.Policies;

/// <summary>
/// 纯托管构建管线:证书字节 → 分组 → 分类 → policy XML → CIP。
/// 不接触文件系统,也不调用 PowerShell:CIP 转换由外部注入的委托完成,
/// 这样 Windows 可以交给 ConfigCI,浏览器可以交给库。
/// </summary>
internal static class PolicyPipeline
{
    /// <summary>
    /// <paramref name="readCertificate"/> 按 certs/ 内的相对路径返回 DER 字节。
    /// <paramref name="convertToCip"/> 把 policy XML 文本转成 CIP 二进制。
    /// </summary>
    internal static IReadOnlyList<PolicyArtifact> Build(
        IEnumerable<string> relativePaths,
        Func<string, byte[]> readCertificate,
        Func<string, byte[]> convertToCip,
        PolicyMode mode,
        out IReadOnlyList<string> warnings)
    {
        var messages = new List<string>();
        var groups = CertScanner.Group(relativePaths);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var artifacts = new List<PolicyArtifact>();

        foreach (var group in groups)
        {
            var classified = group.Files
                .Select(f => (File: f, Cert: ReadOrThrow(f, readCertificate)))
                .Select(x => (
                    x.File,
                    Info: CertClassifier.Classify(x.File.FileName, x.Cert)))
                .ToList();

            var supported = classified.Where(x => x.Info.Supported).ToList();
            var unsupported = classified.Where(x => !x.Info.Supported).Select(x => x.Info).ToList();

            foreach (var item in unsupported)
            {
                messages.Add($"[{group.Name}] 跳过 {item.FileName}:{item.UnsupportedReason}");
            }

            // 没有可用证书的组不生成策略:空策略会占用策略名额,还让人误以为已封禁。
            if (supported.Count == 0)
            {
                messages.Add($"[{group.Name}] 该组没有受支持的证书,已跳过,不生成策略。");
                continue;
            }

            var policyId = PolicyId.ForGroup(group.Name);
            var fileStem = SafeFileName.Unique(group.Name, usedNames);

            // 由证书直接算出 TBS 值,不再经过 Add-SignerRule。
            var certificates = supported.Select(x => x.Info.Certificate).ToList();
            var xml = PolicyXmlBuilder.Build(policyId, group.Name, certificates, mode).ToString();
            var cip = convertToCip(xml);

            artifacts.Add(new PolicyArtifact(
                group.Name,
                policyId,
                fileStem,
                supported.Count,
                unsupported,
                xml,
                cip));
        }

        warnings = messages;
        return artifacts;
    }

    /// <summary>
    /// 证书解析失败时统一包成 <see cref="InvalidOperationException"/>,
    /// 让调用方不必区分底层是 DER 错误还是 IO 错误。
    /// </summary>
    private static CertificateInfo ReadOrThrow(CertFile file, Func<string, byte[]> read)
    {
        try
        {
            return CertReader.Read(read(file.RelativePath));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"无法读取证书 {file.FileName}:{ex.Message}", ex);
        }
    }
}
