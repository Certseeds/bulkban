using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace bulkban.core.tests.Support;

/// <summary>
/// 用 BCL 生成测试证书,并算出期望的 TBS 哈希。
/// 只在测试里用 X509Certificate2:产品代码走自己的 ASN.1 解析,以便在浏览器里运行。
/// </summary>
internal static class TestCertificates
{
    internal static readonly Oid CodeSigningEku = new("1.3.6.1.5.5.7.3.3");

    internal sealed record CertSpec(
        HashAlgorithmName SignatureAlgorithm,
        bool IsCa = false,
        bool WithCodeSigningEku = true,
        DateTimeOffset? NotBefore = null,
        DateTimeOffset? NotAfter = null);

    internal static X509Certificate2 Create(CertSpec? spec = null)
    {
        spec ??= new CertSpec(HashAlgorithmName.SHA256);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=bulkban-test", rsa, spec.SignatureAlgorithm, RSASignaturePadding.Pkcs1);

        if (spec.WithCodeSigningEku)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { CodeSigningEku }, critical: false));
        }

        if (spec.IsCa)
        {
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, 0, critical: true));
        }

        var notBefore = spec.NotBefore ?? DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = spec.NotAfter ?? DateTimeOffset.UtcNow.AddYears(1);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    internal static byte[] Der(CertSpec? spec = null) =>
        Create(spec).Export(X509ContentType.Cert);

    /// <summary>
    /// 用 AsnReader 取出 TBSCertificate 的完整 DER 编码(含 tag+length),
    /// 这是 WDAC CertRoot[Type=TBS] 所哈希的那段。
    /// </summary>
    internal static byte[] TbsOf(byte[] der)
    {
        var reader = new AsnReader(der, AsnEncodingRules.DER);
        var certificate = reader.ReadSequence();
        var tbs = certificate.ReadEncodedValue();
        return tbs.ToArray();
    }

    /// <summary>按证书自身的签名算法选哈希,与 ConfigCI 的行为一致。</summary>
#pragma warning disable CA5350 // 只是为了复现 ConfigCI 对 SHA-1 证书的处理
    internal static string ExpectedTbs(byte[] der)
    {
        var tbs = TbsOf(der);
        var certificate = X509CertificateLoader.LoadCertificate(der);
        var algorithm = certificate.SignatureAlgorithm.FriendlyName?.ToUpperInvariant() ?? string.Empty;
        var hash = algorithm.Contains("SHA512", StringComparison.Ordinal) ? SHA512.HashData(tbs)
            : algorithm.Contains("SHA384", StringComparison.Ordinal) ? SHA384.HashData(tbs)
            : algorithm.Contains("SHA256", StringComparison.Ordinal) ? SHA256.HashData(tbs)
            : SHA1.HashData(tbs);
        return Convert.ToHexString(hash);
    }
#pragma warning restore CA5350

    internal static byte[] WithRsaSha1() => Der(new CertSpec(HashAlgorithmName.SHA1));

    /// <summary>
    /// 从仓库 certs/ 里挑一张签名算法匹配的真实证书。
    /// SHA-1 证书 BCL 已经签不出来,只能用历史证书验证。
    /// </summary>
    internal static (string Path, byte[] Der) FindRealCertificate(string signatureAlgorithmFriendlyName)
    {
        var root = FindRepoCertsDir();
        foreach (var file in Directory.EnumerateFiles(root, "*.cer", SearchOption.AllDirectories))
        {
            var der = File.ReadAllBytes(file);
            using var cert = X509CertificateLoader.LoadCertificate(der);
            var name = cert.SignatureAlgorithm.FriendlyName ?? string.Empty;
            if (name.Equals(signatureAlgorithmFriendlyName, StringComparison.OrdinalIgnoreCase))
            {
                return (file, der);
            }
        }

        throw new InvalidOperationException($"certs/ 里没有 {signatureAlgorithmFriendlyName} 证书");
    }

    private static string FindRepoCertsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "bulkban.slnx")))
        {
            dir = dir.Parent;
        }

        var root = dir?.FullName ?? throw new InvalidOperationException("找不到仓库根目录");
        return Path.Combine(root, "certs");
    }
}
