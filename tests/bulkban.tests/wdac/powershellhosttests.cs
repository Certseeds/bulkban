using System.Text.RegularExpressions;
using bulkban.Wdac;

namespace bulkban.tests.Wdac;

public class PowerShellHostTests
{
    [Fact]
    public void TemplateContainsAllowAllRules()
    {
        var path = Path.Combine(TempDir.Create(), "policy.xml");
        AllowAllTemplate.WriteTo(path);
        var xml = File.ReadAllText(path);

        Assert.Contains("AllowAll", AllowAllTemplate.EmbeddedXml);
        Assert.Equal(2, Regex.Count(xml, "FileName=\"\\*\""));
    }

    [Fact]
    public void EmbeddedTemplateKeepsScriptEnforcementDisabled()
    {
        Assert.Contains("<Option>Disabled:Script Enforcement</Option>", AllowAllTemplate.EmbeddedXml);
        Assert.DoesNotContain("Inherit Default Policy", AllowAllTemplate.EmbeddedXml);
        Assert.DoesNotContain("Revoked Expired As Unsigned", AllowAllTemplate.EmbeddedXml);
    }

    [Fact]
    public void AddSignerAndConvertWhenConfigCiIsAvailable()
    {
        var host = new PowerShellHost();
        if (!host.IsConfigCiAvailable())
        {
            return;
        }

        var dir = TempDir.Create();
        var xml = Path.Combine(dir, "p.xml");
        var cip = Path.Combine(dir, "p.cip");
        var cert = Path.Combine(dir, "test.cer");
        AllowAllTemplate.WriteTo(xml);
        File.WriteAllBytes(cert, TempDir.CreateSelfSignedRsa().Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));

        host.AddDenySigner(xml, cert);
        host.ConvertToCip(xml, cip);

        Assert.True(File.Exists(cip));
        Assert.Contains("DeniedSigners", File.ReadAllText(xml));
    }
}
