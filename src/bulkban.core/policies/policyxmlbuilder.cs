using System.Xml.Linq;
using bulkban.Core.Certificates;

namespace bulkban.Core.Policies;


/// <summary>
/// 直接根据证书构造 WDAC 策略 XML。
/// 之前依赖 ConfigCI 的 <c>Add-SignerRule</c>,那条路径只能在装有 ConfigCI 的 Windows 上跑;
/// 这里按 schema 直接生成,因此浏览器与 exe 共用同一份实现。
/// </summary>
internal static class PolicyXmlBuilder
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";

    /// <summary>Windows 平台 GUID(所有 inbox AllowAll 模板都用这个值)。</summary>
    private const string PlatformId = "{2E07F7E4-194C-4D20-B7C9-6F44A6C5A234}";

    private const string AuditModeOption = "Enabled:Audit Mode";

    /// <summary>
    /// 与 inbox AllowAll 模板保持一致的规则选项。
    /// 不含 Inherit Default Policy / Revoked Expired As Unsigned:这两项会让策略行为不可预期。
    /// </summary>
    private static readonly string[] BaseOptions =
    {
        "Enabled:Unsigned System Integrity Policy",
        "Enabled:Advanced Boot Options Menu",
        "Enabled:UMCI",
        "Enabled:Update Policy No Reboot",
        "Disabled:Script Enforcement",
    };

    internal static XElement Build(
        Guid policyId,
        string policyName,
        IReadOnlyList<CertificateInfo> certificates,
        PolicyMode mode)
    {
        var formattedId = "{" + policyId.ToString().ToUpperInvariant() + "}";

        var root = new XElement(
            Ns + "SiPolicy",
            new XAttribute("xmlns", Ns.NamespaceName),
            new XAttribute("PolicyType", "Base Policy"),
            new XElement(Ns + "VersionEx", "1.0.3.0"),
            new XElement(Ns + "PlatformID", PlatformId),
            BuildRules(mode),
            new XElement(Ns + "EKUs"),
            BuildFileRules(),
            BuildSigners(certificates),
            BuildSigningScenarios(certificates),
            new XElement(Ns + "UpdatePolicySigners"),
            new XElement(Ns + "CiSigners"),
            new XElement(Ns + "HvciOptions", 0),
            BuildSettings(policyName, formattedId),
            new XElement(Ns + "BasePolicyID", formattedId),
            new XElement(Ns + "PolicyID", formattedId));

        return root;
    }

    private static XElement BuildRules(PolicyMode mode)
    {
        var rules = new XElement(Ns + "Rules");
        foreach (var option in BaseOptions)
        {
            rules.Add(Rule(option));
        }

        if (mode == PolicyMode.Audit)
        {
            rules.Add(Rule(AuditModeOption));
        }

        return rules;
    }

    private static XElement Rule(string option) =>
        new(Ns + "Rule", new XElement(Ns + "Option", option));

    /// <summary>两条 AllowAll:一条给驱动场景,一条给用户态场景。先允许全部,再由 deny signer 收窄。</summary>
    private static XElement BuildFileRules() =>
        new(
            Ns + "FileRules",
            new XElement(Ns + "Allow", new XAttribute("ID", "ID_ALLOW_A1"),
                new XAttribute("FriendlyName", ""), new XAttribute("FileName", "*")),
            new XElement(Ns + "Allow", new XAttribute("ID", "ID_ALLOW_A2"),
                new XAttribute("FriendlyName", ""), new XAttribute("FileName", "*")));

    private static XElement BuildSigners(IReadOnlyList<CertificateInfo> certificates)
    {
        var signers = new XElement(Ns + "Signers");
        for (var i = 0; i < certificates.Count; i++)
        {
            signers.Add(new XElement(
                Ns + "Signer",
                new XAttribute("ID", SignerId(i)),
                // Name 只影响可读性,取 TBS 前 8 位足够区分。
                new XAttribute("Name", certificates[i].TbsHex[..8]),
                new XElement(Ns + "CertRoot",
                    new XAttribute("Type", "TBS"),
                    new XAttribute("Value", certificates[i].TbsHex))));
        }

        return signers;
    }

    private static XElement BuildSigningScenarios(IReadOnlyList<CertificateInfo> certificates)
    {
        var drivers = new XElement(
            Ns + "SigningScenario",
            new XAttribute("Value", 131),
            new XAttribute("ID", "ID_SIGNINGSCENARIO_DRIVERS_1"),
            new XAttribute("FriendlyName", "AllowAll"),
            new XElement(Ns + "ProductSigners", FileRulesRef("ID_ALLOW_A1")));

        var windowsProduct = new XElement(Ns + "ProductSigners");
        if (certificates.Count > 0)
        {
            var denied = new XElement(Ns + "DeniedSigners");
            for (var i = 0; i < certificates.Count; i++)
            {
                denied.Add(new XElement(
                    Ns + "DeniedSigner", new XAttribute("SignerId", SignerId(i))));
            }

            windowsProduct.Add(denied);
        }

        windowsProduct.Add(FileRulesRef("ID_ALLOW_A2"));

        var windows = new XElement(
            Ns + "SigningScenario",
            new XAttribute("Value", 12),
            new XAttribute("ID", "ID_SIGNINGSCENARIO_WINDOWS"),
            new XAttribute("FriendlyName", "AllowAll"),
            windowsProduct);

        return new XElement(Ns + "SigningScenarios", drivers, windows);
    }

    private static XElement FileRulesRef(string ruleId) =>
        new(Ns + "FileRulesRef", new XElement(Ns + "FileRuleRef", new XAttribute("RuleID", ruleId)));

    private static XElement BuildSettings(string policyName, string formattedId) =>
        new(
            Ns + "Settings",
            Setting("Name", policyName),
            Setting("Id", formattedId));

    private static XElement Setting(string valueName, string value) =>
        new(
            Ns + "Setting",
            new XAttribute("Provider", "PolicyInfo"),
            new XAttribute("Key", "Information"),
            new XAttribute("ValueName", valueName),
            new XElement(Ns + "Value", new XElement(Ns + "String", value)));

    /// <summary>schema 要求 ID_SIGNER_[A-Z][_A-Z0-9]*。</summary>
    internal static string SignerId(int index) => "ID_SIGNER_S" + (index + 1).ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
}
