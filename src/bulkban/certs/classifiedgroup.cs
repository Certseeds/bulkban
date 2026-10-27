namespace bulkban.Certs;

internal sealed record ClassifiedGroup(
    string Name,
    IReadOnlyList<CertInfo> Supported,
    IReadOnlyList<CertInfo> Unsupported);
