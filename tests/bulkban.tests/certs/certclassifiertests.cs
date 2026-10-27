using bulkban.Certs;

namespace bulkban.tests.Certs;

public class CertClassifierTests
{
    [Fact]
    public void EcdsaIsUnsupported()
    {
        var group = TempDir.GroupWithEcdsaCert();
        var result = CertClassifier.Classify(group);

        Assert.Empty(result.Supported);
        Assert.Single(result.Unsupported);
        Assert.Contains("ECC", result.Unsupported[0].UnsupportedReason);
    }

    [Fact]
    public void DuplicateThumbprintsAreCollapsed()
    {
        var group = TempDir.GroupWithDuplicateRsaCerts();
        var result = CertClassifier.Classify(group);

        Assert.Single(result.Supported);
    }

    [Fact]
    public void CorruptCertificateThrows()
    {
        var group = TempDir.GroupWithCorruptCert();
        Assert.Throws<InvalidOperationException>(() => CertClassifier.Classify(group));
    }

    [Fact]
    public void ExpiredCertificateIsStillSupported()
    {
        var group = TempDir.GroupWithExpiredRsaCert();
        var result = CertClassifier.Classify(group);

        Assert.Single(result.Supported);
    }
}
