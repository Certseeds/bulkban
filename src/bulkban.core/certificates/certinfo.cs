using System.Security.Cryptography;

namespace bulkban.Core.Certificates;

/// <summary>
/// 从 X.509 证书 DER 里取出的、WDAC 需要的字段。
/// <see cref="TbsHex"/> 是对 <c>TBSCertificate</c> 完整 DER 编码求哈希的结果,
/// 算法跟随证书自身的 signatureAlgorithm(与 ConfigCI 行为一致)。
/// </summary>
internal sealed record CertificateInfo(
    string Subject,
    string TbsHex,
    HashAlgorithmName TbsAlgorithm,
    bool HasCodeSigningEku,
    bool IsCertificateAuthority,
    bool IsEcdsa,
    int KeySizeBits,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);
