using System.Security.Cryptography;
using bulkban.Core.Certificates;
using bulkban.core.tests.Support;

namespace bulkban.core.tests.Certificates;

public class CertReaderTests
{
    [Fact]
    public void ReadsTbsHashUsingCertificatesOwnSha256()
    {
        var der = TestCertificates.Der(new TestCertificates.CertSpec(HashAlgorithmName.SHA256));

        var cert = CertReader.Read(der);

        Assert.Equal(TestCertificates.ExpectedTbs(der), cert.TbsHex);
        Assert.Equal(64, cert.TbsHex.Length);          // SHA-256 => 32 bytes
        Assert.Equal(HashAlgorithmName.SHA256, cert.TbsAlgorithm);
    }

    [Fact]
    public void ReadsTbsHashUsingCertificatesOwnSha1()
    {
        // SHA-1 证书 BCL 已经无法签发,用仓库里的真实证书。
        var (path, der) = TestCertificates.FindRealCertificate("sha1RSA");

        var cert = CertReader.Read(der);

        Assert.Equal(TestCertificates.ExpectedTbs(der), cert.TbsHex);
        Assert.Equal(40, cert.TbsHex.Length);          // SHA-1 => 20 bytes
        Assert.Equal(HashAlgorithmName.SHA1, cert.TbsAlgorithm);
        _ = path;
    }

    [Fact]
    public void ReadsTbsHashUsingCertificatesOwnSha384()
    {
        var (_, der) = TestCertificates.FindRealCertificate("sha384RSA");

        var cert = CertReader.Read(der);

        Assert.Equal(TestCertificates.ExpectedTbs(der), cert.TbsHex);
        Assert.Equal(96, cert.TbsHex.Length);          // SHA-384 => 48 bytes
        Assert.Equal(HashAlgorithmName.SHA384, cert.TbsAlgorithm);
    }

    [Fact]
    public void ReportsCodeSigningEku()
    {
        var der = TestCertificates.Der(new TestCertificates.CertSpec(HashAlgorithmName.SHA256));

        var cert = CertReader.Read(der);

        Assert.True(cert.HasCodeSigningEku);
        Assert.False(cert.IsCertificateAuthority);
        Assert.False(cert.IsEcdsa);
    }

    [Fact]
    public void DetectsMissingCodeSigningEku()
    {
        var der = TestCertificates.Der(
            new TestCertificates.CertSpec(HashAlgorithmName.SHA256, WithCodeSigningEku: false));

        Assert.False(CertReader.Read(der).HasCodeSigningEku);
    }

    [Fact]
    public void DetectsCertificateAuthority()
    {
        var der = TestCertificates.Der(
            new TestCertificates.CertSpec(HashAlgorithmName.SHA256, IsCa: true));

        Assert.True(CertReader.Read(der).IsCertificateAuthority);
    }

    [Fact]
    public void ReadsSubjectAndValidity()
    {
        var notBefore = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2030, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var der = TestCertificates.Der(
            new TestCertificates.CertSpec(HashAlgorithmName.SHA256, NotBefore: notBefore, NotAfter: notAfter));

        var cert = CertReader.Read(der);

        Assert.Contains("CN=bulkban-test", cert.Subject, StringComparison.Ordinal);
        Assert.Equal(notBefore, cert.NotBeforeUtc);
        Assert.Equal(notAfter, cert.NotAfterUtc);
    }

    [Fact]
    public void RejectsGarbage()
    {
        var ex = Assert.Throws<InvalidDataException>(
            () => CertReader.Read(new byte[] { 0x30, 0x82, 0x00, 0x00 }));

        Assert.Contains("证书", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsRsaKeySize()
    {
        var der = TestCertificates.Der(new TestCertificates.CertSpec(HashAlgorithmName.SHA256));

        Assert.Equal(2048, CertReader.Read(der).KeySizeBits);
    }
}
