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
        File.Copy(xmlPath, cipPath, overwrite: true);
    }
}
