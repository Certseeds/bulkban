using System.Security.Cryptography;
using System.Xml.Linq;
using bulkban.Core.Certificates;
using bulkban.Core.Policies;
using bulkban.core.tests.Support;

namespace bulkban.core.tests.Policies;

/// <summary>
/// 把生成的 XML 交给真实 ConfigCI 转成 CIP,再反过来校验。
/// 这是「不再依赖 Add-SignerRule」这条新路径的唯一硬证据;缺 ConfigCI 时跳过。
/// </summary>
public class PolicyXmlConfigCiTests
{
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:sipolicy";
    private static readonly Guid PolicyId = new("11111111-2222-4333-8444-555555555555");

    private static bool ConfigCiAvailable()
    {
        var module = Path.Combine(
            SystemRoot, "System32", "WindowsPowerShell", "v1.0", "Modules", "ConfigCI", "ConfigCI.psd1");
        return File.Exists(module);
    }

    /// <summary>用 SystemRoot 拼路径:SpecialFolder.System 有时返回 C:\WINDOWS 这类别名。</summary>
    private static string SystemRoot =>
        Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

    private static string PowerShellPath =>
        Path.Combine(SystemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");

    [Fact]
    public void GeneratedXmlConvertsToCip()
    {
        if (!ConfigCiAvailable())
        {
            return;
        }

        var certs = new[]
        {
            CertReader.Read(TestCertificates.Der()),
            CertReader.Read(TestCertificates.Der()),
        };

        var dir = Directory.CreateTempSubdirectory("bulkban-xml-");
        try
        {
            var xmlPath = Path.Combine(dir.FullName, "policy.xml");
            var cipPath = Path.Combine(dir.FullName, "policy.cip");

            var root = PolicyXmlBuilder.Build(PolicyId, "test-group", certs, PolicyMode.Enforce);
            new XDocument(root).Save(xmlPath);

            var psi = new System.Diagnostics.ProcessStartInfo(PowerShellPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command",
                $"ConvertFrom-CIPolicy -XmlFilePath '{xmlPath}' -BinaryFilePath '{cipPath}'",
            })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = System.Diagnostics.Process.Start(psi)!;
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, $"ConvertFrom-CIPolicy 失败: {stderr}");
            Assert.True(File.Exists(cipPath), "没有生成 CIP");

            var cip = File.ReadAllBytes(cipPath);
            Assert.True(cip.Length > 64);
            // 我们逆向后确认的规则选项位:UMCI(bit2)、Unsigned SIP(bit19)、
            // Advanced Boot Options(bit23)、Script Enforcement(bit24)、No Reboot(bit28)
            var flags = BitConverter.ToUInt32(cip, 0x24);
            Assert.True((flags & 0x00000004) != 0, "UMCI 位未设置");
            Assert.True((flags & 0x00080000) != 0, "Unsigned SIP 位未设置");
            Assert.True((flags & 0x01000000) != 0, "Script Enforcement 位未设置");
            Assert.True((flags & 0x10000000) != 0, "No Reboot 位未设置");
            // Enforce 模式不应有 Audit Mode(bit16)
            Assert.True((flags & 0x00010000) == 0, "Enforce 模式不应带 Audit Mode");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void GeneratedAuditXmlCarriesAuditBit()
    {
        if (!ConfigCiAvailable())
        {
            return;
        }

        var certs = new[] { CertReader.Read(TestCertificates.Der()) };
        var dir = Directory.CreateTempSubdirectory("bulkban-audit-");
        try
        {
            var xmlPath = Path.Combine(dir.FullName, "policy.xml");
            var cipPath = Path.Combine(dir.FullName, "policy.cip");

            new XDocument(PolicyXmlBuilder.Build(PolicyId, "g", certs, PolicyMode.Audit)).Save(xmlPath);

            var psi = new System.Diagnostics.ProcessStartInfo(PowerShellPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command",
                $"ConvertFrom-CIPolicy -XmlFilePath '{xmlPath}' -BinaryFilePath '{cipPath}'",
            })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = System.Diagnostics.Process.Start(psi)!;
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"ConvertFrom-CIPolicy 失败: {stderr}");

            var cip = File.ReadAllBytes(cipPath);
            var flags = BitConverter.ToUInt32(cip, 0x24);
            Assert.True((flags & 0x00010000) != 0, "Audit 模式应带 Audit Mode 位");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void TbsValuesSurviveConversion()
    {
        if (!ConfigCiAvailable())
        {
            return;
        }

        var certs = new[]
        {
            CertReader.Read(TestCertificates.Der()),
            CertReader.Read(TestCertificates.Der()),
        };
        var dir = Directory.CreateTempSubdirectory("bulkban-tbs-");
        try
        {
            var xmlPath = Path.Combine(dir.FullName, "policy.xml");
            var cipPath = Path.Combine(dir.FullName, "policy.cip");

            new XDocument(PolicyXmlBuilder.Build(PolicyId, "g", certs, PolicyMode.Enforce)).Save(xmlPath);

            var psi = new System.Diagnostics.ProcessStartInfo(PowerShellPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command",
                $"ConvertFrom-CIPolicy -XmlFilePath '{xmlPath}' -BinaryFilePath '{cipPath}'",
            })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = System.Diagnostics.Process.Start(psi)!;
            process.WaitForExit();

            // CIP 里应该能找到两张证书的 TBS 十六进制字节
            var cip = File.ReadAllBytes(cipPath);
            var hex = Convert.ToHexString(cip);
            foreach (var cert in certs)
            {
                Assert.Contains(cert.TbsHex, hex, StringComparison.Ordinal);
            }
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
