namespace bulkban.Certs;

internal sealed record CertInfo(
    string FileName,
    string Subject,
    string Sha256,
    bool Supported,
    string? UnsupportedReason);
