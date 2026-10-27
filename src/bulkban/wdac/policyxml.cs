using System.Xml.Linq;

namespace bulkban.Wdac;

internal static class PolicyXml
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";

    private static readonly string[] RemovedRuleOptions =
    {
        "Enabled:Inherit Default Policy",
        "Enabled:Revoked Expired As Unsigned",
    };

    /// <summary>写入 PolicyID、策略名,并移除不需要的规则选项。</summary>
    internal static void Finalize(string xmlPath, Guid policyId, string policyName)
    {
        var doc = XDocument.Load(xmlPath);
        var root = doc.Root ?? throw new InvalidOperationException($"策略 XML 没有根节点:{xmlPath}");
        var formattedId = "{" + policyId.ToString().ToUpperInvariant() + "}";

        SetElementValue(root, "PolicyID", formattedId);
        SetElementValue(root, "BasePolicyID", formattedId);

        PruneRules(root);
        SetPolicyInfo(root, policyName, formattedId);

        doc.Save(xmlPath, SaveOptions.DisableFormatting);
    }

    private static void SetElementValue(XElement root, string name, string value)
    {
        var element = root.Element(Ns + name)
            ?? throw new InvalidOperationException($"策略 XML 缺少 <{name}>");
        element.Value = value;
    }

    private static void PruneRules(XElement root)
    {
        var rules = root.Element(Ns + "Rules");
        if (rules is null)
        {
            return;
        }

        rules.Elements(Ns + "Rule")
            .Where(rule => RemovedRuleOptions.Contains(
                rule.Element(Ns + "Option")?.Value,
                StringComparer.Ordinal))
            .Remove();
    }

    private static void SetPolicyInfo(XElement root, string policyName, string policyId)
    {
        var settings = root.Element(Ns + "Settings");
        if (settings is null)
        {
            return;
        }

        var infoSettings = settings.Elements(Ns + "Setting")
            .Where(s => s.Attribute("Provider")?.Value == "PolicyInfo"
                        && s.Attribute("Key")?.Value == "Information");

        foreach (var setting in infoSettings)
        {
            var stringElement = setting.Element(Ns + "Value")?.Element(Ns + "String");
            if (stringElement is null)
            {
                continue;
            }

            switch (setting.Attribute("ValueName")?.Value)
            {
                case "Name":
                    stringElement.Value = policyName;
                    break;
                case "Id":
                    stringElement.Value = policyId;
                    break;
            }
        }
    }
}
