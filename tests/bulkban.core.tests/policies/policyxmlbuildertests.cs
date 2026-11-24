using System.Security.Cryptography;
using System.Xml.Linq;
using bulkban.Core.Certificates;
using bulkban.Core.Policies;
using bulkban.core.tests.Support;

namespace bulkban.core.tests.Policies;

public class PolicyXmlBuilderTests
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";
    private static readonly Guid PolicyId = new("11111111-2222-4333-8444-555555555555");

    private static XElement Build(
        int certCount = 1,
        PolicyMode mode = PolicyMode.Enforce,
        string name = "group-a")
    {
        var certs = Enumerable.Range(0, certCount)
            .Select(_ => CertReader.Read(TestCertificates.Der()))
            .ToList();
        return PolicyXmlBuilder.Build(PolicyId, name, certs, mode);
    }

    [Fact]
    public void WritesPolicyAndBasePolicyId()
    {
        var root = Build();

        Assert.Equal("{11111111-2222-4333-8444-555555555555}", root.Element(Ns + "PolicyID")!.Value);
        Assert.Equal("{11111111-2222-4333-8444-555555555555}", root.Element(Ns + "BasePolicyID")!.Value);
    }

    [Fact]
    public void EmitsAllowAllRulesForBothScenarios()
    {
        var root = Build();

        var allows = root.Element(Ns + "FileRules")!.Elements(Ns + "Allow").ToList();
        Assert.Equal(2, allows.Count);
        Assert.All(allows, a => Assert.Equal("*", a.Attribute("FileName")!.Value));

        var refs = root.Descendants(Ns + "FileRuleRef").Select(r => r.Attribute("RuleID")!.Value).ToList();
        Assert.Equal(2, refs.Count);
    }

    [Fact]
    public void OneSignerPerCertificateWithTbsValue()
    {
        var certs = new[]
        {
            CertReader.Read(TestCertificates.Der()),
            CertReader.Read(TestCertificates.Der()),
        };
        var root = PolicyXmlBuilder.Build(PolicyId, "g", certs, PolicyMode.Enforce);

        var signers = root.Descendants(Ns + "Signer").ToList();
        Assert.Equal(2, signers.Count);
        Assert.Equal(certs[0].TbsHex, signers[0].Element(Ns + "CertRoot")!.Attribute("Value")!.Value);
        Assert.Equal("TBS", signers[0].Element(Ns + "CertRoot")!.Attribute("Type")!.Value);
    }

    [Fact]
    public void DeniedSignersOnlyInWindowsScenario()
    {
        var root = Build(certCount: 2);

        var scenarios = root.Descendants(Ns + "SigningScenario").ToList();
        var drivers = scenarios.Single(s => s.Attribute("Value")!.Value == "131");
        var windows = scenarios.Single(s => s.Attribute("Value")!.Value == "12");

        Assert.Empty(drivers.Descendants(Ns + "DeniedSigner"));
        Assert.Equal(2, windows.Descendants(Ns + "DeniedSigner").Count());
    }

    [Fact]
    public void AuditModeAddsRuleOption()
    {
        var audit = Build(mode: PolicyMode.Audit);
        var enforce = Build(mode: PolicyMode.Enforce);

        bool HasAudit(XElement root) => root.Descendants(Ns + "Rule")
            .Any(r => r.Element(Ns + "Option")?.Value == "Enabled:Audit Mode");

        Assert.True(HasAudit(audit));
        Assert.False(HasAudit(enforce));
    }

    [Fact]
    public void KeepsUnsignedSystemIntegrityPolicyOption()
    {
        var root = Build();

        Assert.Contains(root.Descendants(Ns + "Option"),
            o => o.Value == "Enabled:Unsigned System Integrity Policy");
    }

    [Fact]
    public void DoesNotInheritDefaultPolicyOrTreatRevokedAsUnsigned()
    {
        var root = Build();

        var options = root.Descendants(Ns + "Option").Select(o => o.Value).ToList();
        Assert.DoesNotContain("Enabled:Inherit Default Policy", options);
        Assert.DoesNotContain("Enabled:Revoked Expired As Unsigned", options);
    }

    [Fact]
    public void SignerIdsSatisfySchema()
    {
        var root = Build(certCount: 12);

        // schema 要求 ID_SIGNER_[A-Z][_A-Z0-9]*
        foreach (var signer in root.Descendants(Ns + "Signer"))
        {
            var id = signer.Attribute("ID")!.Value;
            Assert.Matches("^ID_SIGNER_[A-Z][A-Z0-9_]*$", id);
        }

        foreach (var allow in root.Element(Ns + "FileRules")!.Elements())
        {
            var id = allow.Attribute("ID")!.Value;
            Assert.Matches("^ID_ALLOW_[A-Z][A-Z0-9_]*$", id);
        }
    }

    [Fact]
    public void WritesPolicyInfoName()
    {
        var root = Build(name: "my-group");

        var nameSetting = root.Descendants(Ns + "Setting")
            .Single(s => s.Attribute("ValueName")!.Value == "Name");
        Assert.Equal("my-group", nameSetting.Descendants(Ns + "String").Single().Value);
    }

    [Fact]
    public void IsDeterministic()
    {
        var certs = new[] { CertReader.Read(TestCertificates.Der()) };

        var a = PolicyXmlBuilder.Build(PolicyId, "g", certs, PolicyMode.Audit).ToString();
        var b = PolicyXmlBuilder.Build(PolicyId, "g", certs, PolicyMode.Audit).ToString();

        Assert.Equal(a, b);
    }

    [Fact]
    public void EmptyCertificateListProducesNoDeniedSigners()
    {
        var root = PolicyXmlBuilder.Build(PolicyId, "g", Array.Empty<CertificateInfo>(), PolicyMode.Enforce);

        Assert.Empty(root.Descendants(Ns + "DeniedSigner"));
    }
}
