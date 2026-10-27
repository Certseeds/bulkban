namespace bulkban.Certs;

internal sealed record CertGroup(string Name, IReadOnlyList<CertFile> Files);
