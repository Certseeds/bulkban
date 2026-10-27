using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace bulkban.Certs;

internal static class CertClassifier
{
    private const string RsaOid = "1.2.840.113549.1.1.1";
    private const string CodeSigningEkuOid = "1.3.6.1.5.5.7.3.3";
    private const int MaxRsaKeySize = 4096;

    internal const string EccReason = "ECC signer rules are not supported by WDAC";
    internal const string RsaKeyTooLargeReason = "RSA key size over 4096 bits is not supported by WDAC";
    internal const string CaReason = "CA certificates cannot be used as deny signers";
    internal const string MissingCodeSigningEkuReason = "missing code signing EKU (1.3.6.1.5.5.7.3.3)";

    internal static ClassifiedGroup Classify(CertGroup group)
    {
        var supported = new List<CertInfo>();
        var unsupported = new List<CertInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in group.Files)
        {
            var cert = Load(file);

            if (!seen.Add(ComputeSha256(cert.RawData)))
            {
                continue;
            }

            var info = ToInfo(file, cert, out var reason);
            if (reason is null)
            {
                supported.Add(info);
            }
            else
            {
                unsupported.Add(info with { Supported = false, UnsupportedReason = reason });
            }
        }

        return new ClassifiedGroup(group.Name, supported, unsupported);
    }

    private static X509Certificate2 Load(CertFile file)
    {
        try
        {
            return X509CertificateLoader.LoadCertificateFromFile(file.FullPath);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            throw new InvalidOperationException($"无法读取证书 {file.FileName}:{ex.Message}", ex);
        }
    }

    private static CertInfo ToInfo(CertFile file, X509Certificate2 cert, out string? reason)
    {
        var sha256 = ComputeSha256(cert.RawData);
        var subject = cert.Subject;
        reason = DetectUnsupportedReason(cert);

        return new CertInfo(file.FullPath, file.FileName, subject, sha256, reason is null, reason);
    }

    private static string? DetectUnsupportedReason(X509Certificate2 cert)
    {
        using var publicKey = cert.GetRSAPublicKey();
        if (publicKey is null)
        {
            var ecdsa = cert.GetECDsaPublicKey();
            if (ecdsa is not null)
            {
                ecdsa.Dispose();
                return EccReason;
            }

            return $"不支持的密钥算法:{cert.PublicKey.Oid.Value}";
        }

        if (publicKey.KeySize > MaxRsaKeySize)
        {
            return RsaKeyTooLargeReason;
        }

        var basicConstraints = cert.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();
        if (basicConstraints?.CertificateAuthority == true)
        {
            return CaReason;
        }

        if (!HasCodeSigningEku(cert))
        {
            return MissingCodeSigningEkuReason;
        }

        return null;
    }

    private static bool HasCodeSigningEku(X509Certificate2 cert)
    {
        var eku = cert.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        return eku is not null
            && eku.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == CodeSigningEkuOid);
    }

    private static string ComputeSha256(byte[] rawData)
    {
        return Convert.ToHexString(SHA256.HashData(rawData));
    }
}
