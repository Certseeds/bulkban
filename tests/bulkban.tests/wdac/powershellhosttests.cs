using bulkban.Wdac;

namespace bulkban.tests.Wdac;

public class PowerShellHostTests
{
    [Fact]
    public void ConvertsCoreGeneratedXmlToCip()
    {
        var host = new PowerShellHost();
        if (!host.IsConfigCiAvailable())
        {
            return;
        }

        var dir = TempDir.Create();
        var xml = Path.Combine(dir, "p.xml");
        var cip = Path.Combine(dir, "p.cip");

        var cert = bulkban.Core.Certificates.CertReader.Read(
            TempDir.CreateSelfSignedRsa().Export(
                System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
        var document = bulkban.Core.Policies.PolicyXmlBuilder.Build(
            bulkban.Core.Policies.PolicyId.ForGroup("t"),
            "t",
            new[] { cert },
            PolicyMode.Audit);
        File.WriteAllText(xml, document.ToString());

        host.ConvertToCip(xml, cip);

        Assert.True(File.Exists(cip));
        Assert.Equal(PolicyMode.Audit, CipModeInspector.Detect(cip));
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
