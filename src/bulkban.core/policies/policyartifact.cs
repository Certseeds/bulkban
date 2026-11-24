using bulkban.Core.Certificates;

namespace bulkban.Core.Policies;

/// <summary>一组证书构建出的产物(内存里的结果,磁盘与浏览器共用)。</summary>
internal sealed record PolicyArtifact(
    string GroupName,
    Guid PolicyId,
    string FileStem,
    int CertCount,
    IReadOnlyList<ClassifiedCertificate> Unsupported,
    string Xml,
    byte[] Cip);
