using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using bulkban.Wdac;

namespace bulkban.tests.Wdac;

public class PolicyXmlSchemaTests
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";
    private static readonly string[] ExpectedShortAllowIds = { "ID_ALLOW_A1", "ID_ALLOW_A2" };

    [Fact]
    public void FinalizedTemplateValidatesAgainstCipolicySchema()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);

        PolicyXml.Finalize(path, Guid.Parse("6e013850-2e32-5f22-b069-209ec8df6139"), "2345");

        var errors = ValidateAgainstSchema(path);
        Assert.Empty(errors);
    }

    [Fact]
    public void FinalizedPolicyWithSignersValidatesAgainstSchema()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        AddSigner(path, "ID_SIGNER_S_1_1");
        AddSigner(path, "ID_SIGNER_S_1_1_0");

        PolicyXml.Finalize(path, Guid.Parse("d49ecc6b-5152-58d8-9128-bc288fcf986c"), "360");

        var errors = ValidateAgainstSchema(path);
        Assert.Empty(errors);
    }

    [Fact]
    public void FinalizeCollapsesChainedFileRuleIds()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        AddSigner(path, "ID_SIGNER_S_1_1");
        ChainAllowIds(path, 3);

        PolicyXml.Finalize(path, Guid.NewGuid(), "360");

        var ids = XDocument.Load(path).Root!
            .Element(Ns + "FileRules")!
            .Elements()
            .Select(e => e.Attribute("ID")!.Value)
            .ToList();

        Assert.Equal(ExpectedShortAllowIds, ids);
        var errors = ValidateAgainstSchema(path);
        Assert.Empty(errors);
    }

    [Fact]
    public void FinalizedIdsNeverContainDoubledUnderscoreChains()
    {
        var path = Path.Combine(TempDir.Create(), "p.xml");
        AllowAllTemplate.WriteTo(path);
        AddSigner(path, "ID_SIGNER_S_1_1_0_0_0_0_0_0_0_0_0_0_0_0_0_0_0_0_0_0");

        PolicyXml.Finalize(path, Guid.NewGuid(), "others");

        var xml = File.ReadAllText(path);
        Assert.DoesNotContain("0_0_0_0", xml, StringComparison.Ordinal);
    }

    private static void AddSigner(string path, string signerId)
    {
        var doc = XDocument.Load(path);
        var root = doc.Root!;
        root.Element(Ns + "Signers")!.Add(new XElement(
            Ns + "Signer",
            new XAttribute("ID", signerId),
            new XAttribute("Name", "Company Name"),
            new XElement(Ns + "CertRoot", new XAttribute("Type", "TBS"), new XAttribute("Value", "F6BF44F1ACAE05FC1638190459485DA95F4ED1EC"))));

        var windows = root.Element(Ns + "SigningScenarios")!
            .Elements(Ns + "SigningScenario")
            .Single(s => s.Attribute("Value")?.Value == "12");
        var denied = windows.Element(Ns + "ProductSigners")!.Element(Ns + "DeniedSigners");
        if (denied is null)
        {
            denied = new XElement(Ns + "DeniedSigners");
            windows.Element(Ns + "ProductSigners")!.AddFirst(denied);
        }

        denied.Add(new XElement(Ns + "DeniedSigner", new XAttribute("SignerId", signerId)));
        doc.Save(path, SaveOptions.DisableFormatting);
    }

    private static void ChainAllowIds(string path, int extraLevels)
    {
        var doc = XDocument.Load(path);
        var allow = doc.Root!.Element(Ns + "FileRules")!.Elements().First();
        var id = allow.Attribute("ID")!.Value + string.Concat(Enumerable.Repeat("_0", extraLevels));
        allow.SetAttributeValue("ID", id);
        doc.Save(path, SaveOptions.DisableFormatting);
    }

    internal static IReadOnlyList<string> ValidateAgainstSchema(string xmlPath)
    {
        var schemaPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "schemas",
            "CodeIntegrity",
            "cipolicy.xsd");

        if (!File.Exists(schemaPath))
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
        };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.Schemas.Add(null, schemaPath);
        settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity}: {e.Message}");

        using var reader = XmlReader.Create(xmlPath, settings);
        while (reader.Read())
        {
        }

        return errors;
    }
}
