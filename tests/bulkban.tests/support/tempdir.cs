using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, CreateSelfSignedRsa().Export(X509ContentType.Cert));
        return full;
    }

    internal static string WriteCorruptCer(string root, string relativePath)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[] { 0x30, 0x82, 0x00, 0x00 });
        return full;
    }

    internal static X509Certificate2 CreateSelfSignedRsa()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=bulkban-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var enhancedKeyUsage = new OidCollection { new("1.3.6.1.5.5.7.3.3") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, critical: false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
