using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using bulkban.Certs;

namespace bulkban.tests.Support;

internal static class TempDir
{
    internal static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "bulkban-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    internal static string WriteCer(string root, string relativePath)
    {
        return WriteCertBytes(root, relativePath, CreateSelfSignedRsa().Export(X509ContentType.Cert));
    }

    internal static string WriteCorruptCer(string root, string relativePath)
    {
        return WriteCertBytes(root, relativePath, new byte[] { 0x30, 0x82, 0x00, 0x00 });
    }

    internal static X509Certificate2 CreateSelfSignedRsa()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=bulkban-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var enhancedKeyUsage = new OidCollection { new("1.3.6.1.5.5.7.3.3") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, critical: false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    internal static X509Certificate2 CreateSelfSignedEcdsa()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var request = new CertificateRequest("CN=bulkban-ecc", ecdsa, HashAlgorithmName.SHA384);
        var enhancedKeyUsage = new OidCollection { new("1.3.6.1.5.5.7.3.3") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, critical: false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    internal static X509Certificate2 CreateExpiredRsa()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=bulkban-expired", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var enhancedKeyUsage = new OidCollection { new("1.3.6.1.5.5.7.3.3") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, critical: false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddYears(-3), DateTimeOffset.UtcNow.AddYears(-2));
    }

    internal static string WriteEcdsaCer(string root, string relativePath)
    {
        return WriteCertBytes(root, relativePath, CreateSelfSignedEcdsa().Export(X509ContentType.Cert));
    }

    internal static (string Root, string BaseDir, string CurrentDir) PathsWithCerts()
    {
        var root = Create();
        var baseDir = Path.Combine(root, "app");
        var currentDir = Path.Combine(root, "work");
        Directory.CreateDirectory(Path.Combine(baseDir, "certs"));
        Directory.CreateDirectory(Path.Combine(currentDir, "certs"));
        Directory.CreateDirectory(Path.Combine(root, "explicit"));
        return (root, baseDir, currentDir);
    }

    internal static CertGroup GroupFromBytes(string name, params byte[][] certs)    {
        var root = Create();
        var dir = Path.Combine(root, "certs", name);
        Directory.CreateDirectory(dir);

        var files = new List<CertFile>();
        for (var i = 0; i < certs.Length; i++)
        {
            var path = Path.Combine(dir, $"{i}.cer");
            File.WriteAllBytes(path, certs[i]);
            files.Add(new CertFile(path, Path.GetFileName(path)));
        }

        return new CertGroup(name, files);
    }

    internal static CertGroup GroupWithEcdsaCert()
    {
        return GroupFromBytes("ecc", CreateSelfSignedEcdsa().Export(X509ContentType.Cert));
    }

    internal static CertGroup GroupWithDuplicateRsaCerts()
    {
        var bytes = CreateSelfSignedRsa().Export(X509ContentType.Cert);
        return GroupFromBytes("dup", (byte[])bytes.Clone(), (byte[])bytes.Clone());
    }

    internal static CertGroup GroupWithCorruptCert()
    {
        return GroupFromBytes("corrupt", new byte[] { 0x30, 0x82, 0x00, 0x00 });
    }

    internal static CertGroup GroupWithExpiredRsaCert()
    {
        return GroupFromBytes("expired", CreateExpiredRsa().Export(X509ContentType.Cert));
    }

    private static string WriteCertBytes(string root, string relativePath, byte[] bytes)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
        return full;
    }
}
