using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using bulkban.Core.Certificates;
using bulkban.Core.Policies;
using bulkban.core.tests.Support;

namespace bulkban.core.tests.Policies;

/// <summary>
/// 用仓库 certs/ 下的真实证书跑纯托管路径(解析 → XML → ConfigCI 转 CIP)。
/// 覆盖两点:无 X509Certificate2 也能算对 TBS;生成的 XML 能被 ConfigCI 接受。
/// 不读取任何预生成的产物文件。
/// </summary>
public class RealCertificateTests
{
    private static bool ConfigCiAvailable()
    {
        var module = Path.Combine(
            SystemRoot, "System32", "WindowsPowerShell", "v1.0", "Modules", "ConfigCI", "ConfigCI.psd1");
        return File.Exists(module);
    }

    private static string SystemRoot =>
        Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

    private static string PowerShellPath =>
        Path.Combine(SystemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");

    /// <summary>
    /// 纯托管解析的 TBS,必须与 BCL 对同一张证书算出的值一致。
    /// BCL 只作为测试侧的参照实现,产品代码不用它。
    /// </summary>
    [Fact]
    public void TbsMatchesX509ReferenceOnRealCertificates()
    {
        var files = TestCertificates.RepoCertificateFiles();
        if (files.Count == 0)
        {
            // 证书不进仓库,CI 上没有样本;此时跳过对拍。
            return;
        }

        var checkedCount = 0;
        var mismatches = new List<string>();

        foreach (var file in files)
        {
            byte[] der;
            try
            {
                der = File.ReadAllBytes(file);
                _ = X509CertificateLoader.LoadCertificate(der);
            }
            catch (CryptographicException)
            {
                continue; // 目录里可能混有非证书文件
            }

            var cert = CertReader.Read(der);
            var expected = ExpectedTbs(der);
            checkedCount++;

            if (!string.Equals(cert.TbsHex, expected, StringComparison.Ordinal))
            {
                mismatches.Add($"{Path.GetFileName(file)}: {cert.TbsHex[..16]} != {expected[..16]}");
            }
        }

        Assert.True(checkedCount > 0, "有证书文件但没有一张能解析");
        Assert.Empty(mismatches);
    }

    [Fact]
    public void RealCertificateGroupsProduceConvertibleXml()
    {
        if (!ConfigCiAvailable())
        {
            return;
        }

        var groupDir = Path.Combine(TestCertificates.RepoCertsDir(), "tencent");
        if (!Directory.Exists(groupDir) || !Directory.EnumerateFiles(groupDir, "*.cer").Any())
        {
            return;
        }

        var supported = Directory.EnumerateFiles(groupDir, "*.cer")
            .Select(f => CertReader.Read(File.ReadAllBytes(f)))
            .Where(c => CertClassifier.DetectUnsupportedReason(c) is null)
            .ToList();
        Assert.NotEmpty(supported);

        var cip = ConvertToCip(supported, "tencent");

        // CIP 里必须能找到每张证书的 TBS 字节
        var hex = Convert.ToHexString(cip);
        Assert.All(supported, c => Assert.Contains(c.TbsHex, hex, StringComparison.Ordinal));
        Assert.True(cip.Length > 64);
    }

    private static byte[] ConvertToCip(IReadOnlyList<CertificateInfo> certs, string name)
    {
        var dir = Directory.CreateTempSubdirectory("bulkban-real-");
        try
        {
            var xmlPath = Path.Combine(dir.FullName, "p.xml");
            var cipPath = Path.Combine(dir.FullName, "p.cip");

            new XDocument(PolicyXmlBuilder.Build(Guid.NewGuid(), name, certs, PolicyMode.Enforce))
                .Save(xmlPath);

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

            return File.ReadAllBytes(cipPath);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>测试侧的参照实现:按证书自身的签名算法选哈希。</summary>
    private static string ExpectedTbs(byte[] der)
    {
        var tbs = TbsOf(der);
        using var certificate = X509CertificateLoader.LoadCertificate(der);
        var name = certificate.SignatureAlgorithm.FriendlyName ?? string.Empty;

#pragma warning disable CA5350 // 复现 ConfigCI 对 SHA-1 证书的处理
        var hash = name.Contains("SHA512", StringComparison.OrdinalIgnoreCase) ? SHA512.HashData(tbs)
            : name.Contains("SHA384", StringComparison.OrdinalIgnoreCase) ? SHA384.HashData(tbs)
            : name.Contains("SHA256", StringComparison.OrdinalIgnoreCase) ? SHA256.HashData(tbs)
            : SHA1.HashData(tbs);
#pragma warning restore CA5350
        return Convert.ToHexString(hash);
    }

    private static byte[] TbsOf(byte[] der)
    {
        var reader = new System.Formats.Asn1.AsnReader(der, System.Formats.Asn1.AsnEncodingRules.DER);
        var certificate = reader.ReadSequence();
        return certificate.ReadEncodedValue().ToArray();
    }

}
