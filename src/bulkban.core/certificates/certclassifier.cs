namespace bulkban.Core.Certificates;

/// <summary>
/// 判断一张证书能否作为 WDAC 的 deny signer。
/// 规则来自 WDAC 的硬性限制,不是本项目自选的策略。
/// </summary>
internal static class CertClassifier
{
    internal const int MaxRsaKeySize = 4096;

    internal const string EccReason = "ECC signer rules are not supported by WDAC";
    internal const string RsaKeyTooLargeReason = "RSA key size over 4096 bits is not supported by WDAC";
    internal const string CaReason = "CA certificates cannot be used as deny signers";
    internal const string MissingCodeSigningEkuReason = "missing code signing EKU (1.3.6.1.5.5.7.3.3)";

    internal static ClassifiedCertificate Classify(string fileName, CertificateInfo cert)
    {
        var reason = DetectUnsupportedReason(cert);
        return new ClassifiedCertificate(fileName, cert, reason is null, reason);
    }

    internal static string? DetectUnsupportedReason(CertificateInfo cert)
    {
        if (cert.IsEcdsa)
        {
            return EccReason;
        }

        if (cert.KeySizeBits > MaxRsaKeySize)
        {
            return RsaKeyTooLargeReason;
        }

        if (cert.IsCertificateAuthority)
        {
            return CaReason;
        }

        if (!cert.HasCodeSigningEku)
        {
            return MissingCodeSigningEkuReason;
        }

        return null;
    }
}
