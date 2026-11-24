using System.Text;
using bulkban.Core.Certificates;
using bulkban.Core.Policies;
using bulkban.core.tests.Support;

namespace bulkban.core.tests.Policies;

public class PolicyPipelineTests
{
    private static readonly string[] ExpectedGroups = { "a", "b", "others" };

    /// <summary>假的 CIP 转换:把 XML 包一层,便于断言转换被调用过。</summary>
    private static byte[] FakeCip(string xml) => Encoding.UTF8.GetBytes("CIP:" + xml.Length);

    [Fact]
    public void GroupsCertificatesAndProducesArtifacts()
    {
        var certs = new Dictionary<string, byte[]>
        {
            ["a/1.cer"] = TestCertificates.Der(),
            ["b/2.cer"] = TestCertificates.Der(),
            ["3.cer"] = TestCertificates.Der(),
        };

        var artifacts = PolicyPipeline.Build(
            certs.Keys,
            p => certs[p],
            FakeCip,
            PolicyMode.Enforce,
            out var warnings);

        Assert.Equal(ExpectedGroups, artifacts.Select(a => a.GroupName));
        Assert.All(artifacts, a => Assert.Equal(1, a.CertCount));
        Assert.Empty(warnings);
    }

    [Fact]
    public void SkipsGroupsWithoutSupportedCertificates()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP384);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=ecc", ecdsa, System.Security.Cryptography.HashAlgorithmName.SHA384);
        var eccDer = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1))
            .Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert);

        var certs = new Dictionary<string, byte[]>
        {
            ["ecc/1.cer"] = eccDer,
            ["rsa/1.cer"] = TestCertificates.Der(),
        };

        var artifacts = PolicyPipeline.Build(
            certs.Keys, p => certs[p], FakeCip, PolicyMode.Enforce, out var warnings);

        Assert.Single(artifacts);
        Assert.Equal("rsa", artifacts[0].GroupName);
        Assert.Contains(warnings, w => w.Contains("ecc", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("没有受支持的证书", StringComparison.Ordinal));
    }

    [Fact]
    public void PolicyIdIsDeterministicPerGroup()
    {
        var certs = new Dictionary<string, byte[]> { ["g/1.cer"] = TestCertificates.Der() };

        var first = PolicyPipeline.Build(
            certs.Keys, p => certs[p], FakeCip, PolicyMode.Audit, out _);
        var second = PolicyPipeline.Build(
            certs.Keys, p => certs[p], FakeCip, PolicyMode.Audit, out _);

        Assert.Equal(first[0].PolicyId, second[0].PolicyId);
        Assert.Equal(PolicyId.ForGroup("g"), first[0].PolicyId);
    }

    [Fact]
    public void AuditAndEnforceProduceDifferentXml()
    {
        var certs = new Dictionary<string, byte[]> { ["g/1.cer"] = TestCertificates.Der() };

        var audit = PolicyPipeline.Build(
            certs.Keys, p => certs[p], FakeCip, PolicyMode.Audit, out _);
        var enforce = PolicyPipeline.Build(
            certs.Keys, p => certs[p], FakeCip, PolicyMode.Enforce, out _);

        Assert.Contains("Enabled:Audit Mode", audit[0].Xml, StringComparison.Ordinal);
        Assert.DoesNotContain("Enabled:Audit Mode", enforce[0].Xml, StringComparison.Ordinal);
    }

    [Fact]
    public void CorruptCertificateThrowsInvalidOperation()
    {
        var certs = new Dictionary<string, byte[]> { ["g/1.cer"] = new byte[] { 0x30, 0x82, 0, 0 } };

        var ex = Assert.Throws<InvalidOperationException>(
            () => PolicyPipeline.Build(
                certs.Keys, p => certs[p], FakeCip, PolicyMode.Enforce, out _));

        Assert.Contains("无法读取证书", ex.Message, StringComparison.Ordinal);
    }
}
