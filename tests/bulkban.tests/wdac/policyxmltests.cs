using System.Xml.Linq;
using bulkban.Wdac;

namespace bulkban.tests.Wdac;

public class PolicyXmlTests
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";

    [Fact]
    public void FinalizeSetsIdsAndPrunesRules()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        var id = Guid.Parse("11111111-2222-4333-8444-555555555555");

        PolicyXml.Finalize(path, id, "others");

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

        PolicyXml.Finalize(path, Guid.NewGuid(), "360");

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

        PolicyXml.Finalize(path, Guid.NewGuid(), "tencent");

        var doc = XDocument.Load(path);
        var nameSetting = doc.Root!
            .Element(Ns + "Settings")!
            .Elements(Ns + "Setting")
            .Single(s => s.Attribute("ValueName")?.Value == "Name");
        Assert.Equal("tencent", nameSetting.Element(Ns + "Value")!.Element(Ns + "String")!.Value);
    }
}
