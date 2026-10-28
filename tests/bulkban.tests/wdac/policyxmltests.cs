using System.Text.RegularExpressions;
using System.Xml.Linq;
using bulkban.Wdac;

namespace bulkban.tests.Wdac;

public class PolicyXmlTests
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";
    private static readonly string[] ExpectedRenumberedIds = { "ID_SIGNER_S001", "ID_SIGNER_S002", "ID_SIGNER_S003" };

    [Fact]
    public void FinalizeSetsIdsAndPrunesRules()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        var id = Guid.Parse("11111111-2222-4333-8444-555555555555");

        PolicyXml.Finalize(path, id, "others", PolicyMode.Audit);

        var doc = XDocument.Load(path);
        Assert.Equal("{11111111-2222-4333-8444-555555555555}", doc.Root!.Element(Ns + "PolicyID")!.Value);
        Assert.Equal("{11111111-2222-4333-8444-555555555555}", doc.Root.Element(Ns + "BasePolicyID")!.Value);
        Assert.DoesNotContain("Inherit Default Policy", File.ReadAllText(path));
        Assert.DoesNotContain("Revoked Expired As Unsigned", File.ReadAllText(path));
        Assert.Contains("Disabled:Script Enforcement", File.ReadAllText(path));
        Assert.Contains("others", File.ReadAllText(path));
    }

    [Fact]
    public void FinalizeKeepsAllowAllRulesAndScenarios()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);

        var doc = XDocument.Load(path);
        var fileRules = doc.Root!.Element(Ns + "FileRules")!;
        Assert.Equal(2, fileRules.Elements(Ns + "Allow").Count());
        Assert.Equal(2, doc.Root.Element(Ns + "SigningScenarios")!.Elements(Ns + "SigningScenario").Count());
    }

    [Fact]
    public void FinalizeSetsPolicyInfoName()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);

        PolicyXml.Finalize(path, Guid.NewGuid(), "tencent", PolicyMode.Audit);

        var doc = XDocument.Load(path);
        var nameSetting = doc.Root!
            .Element(Ns + "Settings")!
            .Elements(Ns + "Setting")
            .Single(s => s.Attribute("ValueName")?.Value == "Name");
        Assert.Equal("tencent", nameSetting.Element(Ns + "Value")!.Element(Ns + "String")!.Value);
    }

    [Fact]
    public void FinalizeRenumbersSignerIds()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        AddChainSigners(path, 3);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);

        var doc = XDocument.Load(path);
        var signerIds = doc.Root!.Element(Ns + "Signers")!
            .Elements(Ns + "Signer")
            .Select(s => s.Attribute("ID")!.Value)
            .ToList();

        Assert.Equal(ExpectedRenumberedIds, signerIds);

        var referenced = doc.Descendants(Ns + "DeniedSigner")
            .Concat(doc.Descendants(Ns + "CiSigner"))
            .Select(s => s.Attribute("SignerId")!.Value)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(ExpectedRenumberedIds, referenced);
    }

    [Fact]
    public void FinalizeShortensSignerNameToValuePrefix()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        AddChainSigners(path, 2);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);

        var doc = XDocument.Load(path);
        var signers = doc.Root!.Element(Ns + "Signers")!.Elements(Ns + "Signer").ToList();

        Assert.Equal("ABCD0", signers[0].Attribute("Name")!.Value);
        Assert.Equal("ABCD1", signers[1].Attribute("Name")!.Value);
    }

    [Fact]
    public void FinalizeTruncatesLongValueToEightChars()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        AddChainSigners(path, 1);
        var doc = XDocument.Load(path);
        doc.Root!.Element(Ns + "Signers")!.Element(Ns + "Signer")!
            .Element(Ns + "CertRoot")!
            .SetAttributeValue("Value", "F6BF44F1ACAE05FC1638190459485DA95F4ED1EC");
        doc.Save(path, SaveOptions.DisableFormatting);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);

        var signer = XDocument.Load(path).Root!.Element(Ns + "Signers")!.Element(Ns + "Signer")!;
        Assert.Equal("F6BF44F1", signer.Attribute("Name")!.Value);
    }

    [Fact]
    public void FinalizeKeepsCertRootValues()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        AddChainSigners(path, 2);
        var before = XDocument.Load(path).Descendants(Ns + "CertRoot")
            .Select(c => c.Attribute("Value")!.Value)
            .ToList();

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);

        var after = XDocument.Load(path).Descendants(Ns + "CertRoot")
            .Select(c => c.Attribute("Value")!.Value)
            .ToList();
        Assert.Equal(before, after);
    }

    [Fact]
    public void FinalizeAuditModeAddsAuditRule()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);

        Assert.Contains("Enabled:Audit Mode", File.ReadAllText(path));
    }

    [Fact]
    public void FinalizeEnforceModeRemovesAuditRule()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        var doc = XDocument.Load(path);
        doc.Root!.Element(Ns + "Rules")!.Add(
            new XElement(Ns + "Rule", new XElement(Ns + "Option", "Enabled:Audit Mode")));
        doc.Save(path, SaveOptions.DisableFormatting);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Enforce);

        Assert.DoesNotContain("Enabled:Audit Mode", File.ReadAllText(path));
    }

    [Fact]
    public void FinalizeEnforceModeKeepsSingleAuditRuleWhenAlreadyAbsent()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Enforce);
        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Enforce);

        Assert.DoesNotContain("Enabled:Audit Mode", File.ReadAllText(path));
    }

    [Fact]
    public void FinalizeAuditModeDoesNotDuplicateRule()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);
        PolicyXml.Finalize(path, Guid.NewGuid(), "360", PolicyMode.Audit);

        var xml = File.ReadAllText(path);
        Assert.Equal(1, Regex.Count(xml, "Enabled:Audit Mode"));
    }

    /// <summary>模拟 Add-SignerRule 的链式 ID:`X`、`X_0`、`X_0_0`…</summary>
    private static void AddChainSigners(string path, int count)
    {
        var doc = XDocument.Load(path);
        var root = doc.Root!;
        var signers = root.Element(Ns + "Signers")!;
        var windows = root.Element(Ns + "SigningScenarios")!
            .Elements(Ns + "SigningScenario")
            .Single(s => s.Attribute("Value")?.Value == "12");
        var productSigners = windows.Element(Ns + "ProductSigners")!;
        var denied = new XElement(Ns + "DeniedSigners");
        productSigners.Add(denied);

        var current = "ID_SIGNER_S_1_1";
        for (var i = 0; i < count; i++)
        {
            signers.Add(new XElement(
                Ns + "Signer",
                new XAttribute("ID", current),
                new XAttribute("Name", "Subject " + i),
                new XElement(Ns + "CertRoot", new XAttribute("Type", "TBS"), new XAttribute("Value", "ABCD" + i))));
            denied.Add(new XElement(Ns + "DeniedSigner", new XAttribute("SignerId", current)));
            current += "_0";
        }

        doc.Save(path, SaveOptions.DisableFormatting);
    }
}
