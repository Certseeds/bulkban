namespace bulkban.Certs;

internal sealed record CertInfo(
    string FullPath,
    string FileName,
    string Subject,
    string Sha256,
    bool Supported,
    string? UnsupportedReason);
