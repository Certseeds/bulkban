namespace bulkban.Core.Certificates;

/// <summary>一张待处理的证书文件。名称是它在 certs/ 内的相对路径(用 '/' 分隔)。</summary>
internal sealed record CertFile(string RelativePath, string FileName);
