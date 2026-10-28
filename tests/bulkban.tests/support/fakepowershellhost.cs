using System.Text;
using bulkban.Wdac;

namespace bulkban.tests.Support;

/// <summary>不依赖 ConfigCI 的测试替身:只做文件级操作,够验证管线。</summary>
internal sealed class FakePowerShellHost : IPowerShellHost
{
    public List<string> AddedCertificates { get; } = new();

    public bool IsConfigCiAvailable() => true;

    public void AddDenySigner(string xmlPath, string certPath)
    {
        if (!File.Exists(xmlPath) || !File.Exists(certPath))
        {
            throw new InvalidOperationException("测试替身:输入文件不存在");
        }

        AddedCertificates.Add(certPath);
        File.AppendAllText(xmlPath, $"<!-- signer {Path.GetFileName(certPath)} -->");
    }

    public void ConvertToCip(string xmlPath, string cipPath)
    {
        // 模拟真实 CIP 头部:offset 0x26 的 bit 0 表示 Audit Mode,
        // 其余内容附带 XML,便于断言 hash 行为。
        var xml = File.ReadAllText(xmlPath);
        var xmlBytes = Encoding.UTF8.GetBytes(xml);
        var bytes = new byte[64 + xmlBytes.Length];
        bytes[0x26] = xml.Contains("Enabled:Audit Mode", StringComparison.Ordinal)
            ? (byte)0x89
            : (byte)0x88;
        xmlBytes.CopyTo(bytes, 64);
        File.WriteAllBytes(cipPath, bytes);
    }
}
