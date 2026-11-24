namespace bulkban.Core.Certificates;

/// <summary>一个证书组;组名对应输出里的 policy 文件名。</summary>
internal sealed record CertGroup(string Name, IReadOnlyList<CertFile> Files);
