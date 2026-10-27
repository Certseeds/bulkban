namespace bulkban.Store;

internal sealed record Manifest(
    int SchemaVersion,
    string ToolVersion,
    string GeneratedAt,
    string CertsDir,
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
