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

    [Fact]
    public void WrappedScriptForcesUtf8Output()
    {
        var script = PowerShellHost.WrapScript("Write-Output 1");

        Assert.Contains("[Console]::OutputEncoding", script, StringComparison.Ordinal);
        Assert.Contains("UTF8", script, StringComparison.Ordinal);
        Assert.Contains("Write-Output 1", script, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureMessageIncludesContext()
    {
        var message = PowerShellHost.FormatFailure(
            "ConvertFrom-CIPolicy",
            1,
            @"C:\tmp\others.xml",
            "boom");

        Assert.Contains("ConvertFrom-CIPolicy", message, StringComparison.Ordinal);
        Assert.Contains(@"C:\tmp\others.xml", message, StringComparison.Ordinal);
        Assert.Contains("boom", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConversionFailureNamesTheXmlFile()
    {
        var dir = TempDir.Create();
        var xml = Path.Combine(dir, "missing.xml");
        var cip = Path.Combine(dir, "missing.cip");
        var host = new PowerShellHost();

        var ex = Assert.Throws<InvalidOperationException>(() => host.ConvertToCip(xml, cip));

        Assert.Contains(xml, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunScriptEmitsUtf8ForChineseErrors()
    {
        var result = PowerShellHost.RunScript("Write-Error '中文错误'; exit 1");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("中文错误", result.StandardError, StringComparison.Ordinal);
    }
}
