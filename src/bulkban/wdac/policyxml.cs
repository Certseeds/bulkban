using System.Globalization;
using System.Xml.Linq;

namespace bulkban.Wdac;

internal static class PolicyXml
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";

    private const int SignerNameLength = 8;

    private static readonly string[] RemovedRuleOptions =
    {
        "Enabled:Inherit Default Policy",
        "Enabled:Revoked Expired As Unsigned",
    };

    /// <summary>
    /// 在 XML 转 CIP **之前**调用:写入 PolicyID、策略名,移除规则选项,并规范化 ID。
    /// </summary>
    internal static void Finalize(string xmlPath, Guid policyId, string policyName)
    {
        var doc = XDocument.Load(xmlPath);
        var root = doc.Root ?? throw new InvalidOperationException($"策略 XML 没有根节点:{xmlPath}");
        var formattedId = "{" + policyId.ToString().ToUpperInvariant() + "}";

        SetElementValue(root, "PolicyID", formattedId);
        SetElementValue(root, "BasePolicyID", formattedId);

        PruneRules(root);
        NormalizeFileRuleIds(root);
        NormalizeSigners(root);
        SetPolicyInfo(root, policyName, formattedId);

        doc.Save(xmlPath, SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// ConfigCI 的 ID 生成器是链式的(`A_1` → `A_1_0` → `A_1_0_0`…)。
    /// schema 要求 `ID_ALLOW_[A-Z][_A-Z0-9]*`,数字开头不合法,这里改成 `ID_ALLOW_A1` 这类短 ID。
    /// </summary>
    private static void NormalizeFileRuleIds(XElement root)
    {
        var fileRules = root.Element(Ns + "FileRules");
        if (fileRules is null)
        {
            return;
        }

        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (prefix, rule) in Categories())
        {
            var index = 0;
            foreach (var element in fileRules.Elements(Ns + rule).ToList())
            {
                var oldId = element.Attribute("ID")?.Value;
                if (string.IsNullOrEmpty(oldId))
                {
                    continue;
                }

                index++;
                var newId = prefix + index.ToString("D1", CultureInfo.InvariantCulture);
                mapping[oldId] = newId;
                element.SetAttributeValue("ID", newId);
            }
        }

        RewriteReferences(root, "RuleID", mapping);

        static (string Prefix, string Element)[] Categories() =>
            new[] { ("ID_ALLOW_A", "Allow"), ("ID_DENY_D", "Deny"), ("ID_FILE_F", "FileAttrib") };
    }

    /// <summary>
    /// ConfigCI 的 signer ID 同样是链式增长,160 张证书会膨胀到 300+ 字符。
    /// 统一改成 `ID_SIGNER_S001`(下划线后必须是大写字母),并把 Name 改成 CertRoot Value 前 8 位。
    /// </summary>
    private static void NormalizeSigners(XElement root)
    {
        var signers = root.Element(Ns + "Signers");
        if (signers is null)
        {
            return;
        }

        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var signer in signers.Elements(Ns + "Signer"))
        {
            var oldId = signer.Attribute("ID")?.Value;
            if (string.IsNullOrEmpty(oldId))
            {
                continue;
            }

            index++;
            var newId = "ID_SIGNER_S" + index.ToString("D3", CultureInfo.InvariantCulture);
            mapping[oldId] = newId;

            signer.SetAttributeValue("ID", newId);

            var value = signer.Element(Ns + "CertRoot")?.Attribute("Value")?.Value;
            if (!string.IsNullOrEmpty(value))
            {
                signer.SetAttributeValue(
                    "Name",
                    value.Length <= SignerNameLength ? value : value[..SignerNameLength]);
            }
        }

        RewriteReferences(root, "SignerId", mapping);
    }

    private static void RewriteReferences(XElement root, string attributeName, Dictionary<string, string> mapping)
    {
        if (mapping.Count == 0)
        {
            return;
        }

        foreach (var element in root.Descendants())
        {
            var attribute = element.Attribute(attributeName);
            if (attribute is not null && mapping.TryGetValue(attribute.Value, out var replacement))
            {
                attribute.Value = replacement;
            }
        }
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
