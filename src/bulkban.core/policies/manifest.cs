namespace bulkban.Core.Policies;

/// <summary>
/// 一次构建的清单:记录每个组的 PolicyID、产物相对路径与 SHA-256。
/// 放在 core 是为了让浏览器端也产出同样格式的文件,exe 的 install/verify 能直接吃。
/// </summary>
internal sealed record Manifest(
    int SchemaVersion,
    string ToolVersion,
    string GeneratedAt,
    string CertsDir,
    string Mode,
    IReadOnlyList<ManifestGroup> Groups);

internal sealed record ManifestGroup(
    string Name,
    string PolicyId,
    int CertCount,
    IReadOnlyList<UnsupportedCert> Unsupported,
    string Xml,
    string XmlSha256,
    string Cip,
    string CipSha256);

internal sealed record UnsupportedCert(string File, string Subject, string Reason);
