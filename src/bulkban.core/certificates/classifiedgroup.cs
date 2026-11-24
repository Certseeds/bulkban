namespace bulkban.Core.Certificates;

/// <summary>分类结果:能用作 WDAC deny signer 的,与被跳过的(附原因)。</summary>
internal sealed record ClassifiedCertificate(
    string FileName,
    CertificateInfo Certificate,
    bool Supported,
    string? UnsupportedReason)
{
    internal string Subject => Certificate.Subject;

    internal string TbsHex => Certificate.TbsHex;
}
