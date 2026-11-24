using System.Globalization;
using System.Security.Cryptography;
using bulkban.Core.Asn1;

namespace bulkban.Core.Certificates;

/// <summary>
/// 纯托管的 X.509 解析。刻意不使用 <c>X509Certificate2</c>:
/// 它在 browser 平台被标注为不支持,拿不到 TBS 的原始编码,也无法控制哈希算法。
/// </summary>
internal static class CertReader
{
    private const string CodeSigningEkuOid = "1.3.6.1.5.5.7.3.3";
    private const string RsaOid = "1.2.840.113549.1.1.1";

    private static readonly string[] Sha512SignatureOids =
    {
        "1.2.840.113549.1.1.13", // sha512WithRSAEncryption
        "1.2.840.10045.4.3.4",   // ecdsa-with-SHA512
    };

    private static readonly string[] Sha384SignatureOids =
    {
        "1.2.840.113549.1.1.12", // sha384WithRSAEncryption
        "1.2.840.10045.4.3.3",   // ecdsa-with-SHA384
    };

    private static readonly string[] Sha256SignatureOids =
    {
        "1.2.840.113549.1.1.11", // sha256WithRSAEncryption
        "1.2.840.10045.4.3.2",   // ecdsa-with-SHA256
    };

    /// <summary>解析失败时抛 <see cref="InvalidDataException"/>。</summary>
    internal static CertificateInfo Read(ReadOnlyMemory<byte> der)
    {
        try
        {
            return Parse(der);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"证书解析失败:{ex.Message}", ex);
        }
    }

    internal static CertificateInfo ReadFile(string path) => Read(File.ReadAllBytes(path));

    private static CertificateInfo Parse(ReadOnlyMemory<byte> der)
    {
        var offset = 0;
        var certificate = DerReader.Read(der, ref offset, der.Length);
        if (!certificate.IsSequence)
        {
            throw new InvalidDataException("证书不是 DER SEQUENCE。");
        }

        var parts = DerReader.ReadChildren(certificate);
        if (parts.Count < 3)
        {
            throw new InvalidDataException($"证书结构不完整:只有 {parts.Count} 个字段。");
        }

        var tbs = parts[0];
        var signatureAlgorithm = parts[1];
        if (!tbs.IsSequence || !signatureAlgorithm.IsSequence)
        {
            throw new InvalidDataException("证书的 TBS 或签名算法不是 SEQUENCE。");
        }

        var signatureOid = ReadAlgorithmOid(signatureAlgorithm);
        var tbsAlgorithm = HashForSignatureOid(signatureOid);
        var tbsHex = Convert.ToHexString(HashTbs(tbs.Raw, tbsAlgorithm));

        var tbsFields = DerReader.ReadChildren(tbs);
        var (subject, validity) = ParseTbsFields(tbsFields);
        var publicKey = FindSubjectPublicKeyInfo(tbsFields);

        var keyOid = publicKey is null ? string.Empty : ReadAlgorithmOid(publicKey.Value.algorithm);
        var isRsa = keyOid == RsaOid;
        var keyBits = isRsa && publicKey is not null ? ReadRsaKeyBits(publicKey.Value.keyBits) : 0;

        var extensions = ReadExtensions(tbsFields);

        return new CertificateInfo(
            subject,
            tbsHex,
            tbsAlgorithm,
            extensions.Ekus.Contains(CodeSigningEkuOid, StringComparer.Ordinal),
            extensions.CertificateAuthority,
            !isRsa,
            keyBits,
            validity.NotBefore,
            validity.NotAfter);
    }

    // SHA-1 只用于匹配 ConfigCI 对 SHA-1 证书签名的处理方式,不是用来做安全校验。
#pragma warning disable CA5350
    private static byte[] HashTbs(ReadOnlyMemory<byte> tbs, HashAlgorithmName algorithm) =>
        algorithm == HashAlgorithmName.SHA512 ? SHA512.HashData(tbs.Span)
        : algorithm == HashAlgorithmName.SHA384 ? SHA384.HashData(tbs.Span)
        : algorithm == HashAlgorithmName.SHA256 ? SHA256.HashData(tbs.Span)
        : SHA1.HashData(tbs.Span);

    private static HashAlgorithmName HashForSignatureOid(string oid) =>
        Array.IndexOf(Sha512SignatureOids, oid) >= 0 ? HashAlgorithmName.SHA512
        : Array.IndexOf(Sha384SignatureOids, oid) >= 0 ? HashAlgorithmName.SHA384
        : Array.IndexOf(Sha256SignatureOids, oid) >= 0 ? HashAlgorithmName.SHA256
        : HashAlgorithmName.SHA1;
#pragma warning restore CA5350

    private static string ReadAlgorithmOid(Asn1Element algorithm)
    {
        var children = DerReader.ReadChildren(algorithm);
        return children.Count > 0 ? DerReader.ReadOid(children[0]) : string.Empty;
    }

    private static (string Subject, (DateTimeOffset NotBefore, DateTimeOffset NotAfter) Validity)
        ParseTbsFields(List<Asn1Element> fields)
    {
        string subject = string.Empty;
        var validity = (DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        foreach (var field in fields)
        {
            if (!field.IsSequence)
            {
                continue;
            }

            var children = DerReader.ReadChildren(field);
            if (children.Count == 0)
            {
                continue;
            }

            // Validity: SEQUENCE { notBefore Time, notAfter Time }
            if (children.Count == 2 && children[0].Tag is 0x17 or 0x18 && children[1].Tag is 0x17 or 0x18)
            {
                validity = (ReadTime(children[0]), ReadTime(children[1]));
                continue;
            }

            // Name: SEQUENCE OF SET OF SEQUENCE {OID, value}
            // RDN 数量不定(单个 CN 时只有 1 个),不能按子元素个数判断。
            if (subject.Length == 0 && children[0].Tag == 0x31)
            {
                subject = ReadName(field);
            }
        }

        return (subject, validity);
    }

    private static string ReadName(Asn1Element name)
    {
        var parts = new List<string>();
        foreach (var rdn in DerReader.ReadChildren(name))
        {
            foreach (var attribute in DerReader.ReadChildren(rdn))
            {
                var pair = DerReader.ReadChildren(attribute);
                if (pair.Count < 2)
                {
                    continue;
                }

                var type = DerReader.ReadOid(pair[0]);
                var value = pair[1].AsString();
                if (value is null)
                {
                    continue;
                }

                parts.Add($"{ShortName(type)}={value}");
            }
        }

        return string.Join(", ", parts);
    }

    private static string ShortName(string oid) => oid switch
    {
        "2.5.4.3" => "CN",
        "2.5.4.6" => "C",
        "2.5.4.7" => "L",
        "2.5.4.8" => "S",
        "2.5.4.10" => "O",
        "2.5.4.11" => "OU",
        _ => oid,
    };

    private static DateTimeOffset ReadTime(Asn1Element element)
    {
        var text = System.Text.Encoding.ASCII.GetString(element.Content.Span);
        var formats = element.Tag == 0x17
            ? new[] { "yyMMddHHmmsszzz", "yyMMddHHmm'Z'" }
            : new[] { "yyyyMMddHHmmsszzz", "yyyyMMddHHmmss'Z'" };

        foreach (var format in formats)
        {
            var normalized = text.Replace('Z', '+').TrimEnd('+');
            if (DateTimeOffset.TryParseExact(
                    normalized, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        // UTCTime 只精确到秒且以 Z 结尾是常态,单独处理。
        var stripped = text.TrimEnd('Z');
        var pattern = element.Tag == 0x17 ? "yyMMddHHmmss" : "yyyyMMddHHmmss";
        if (DateTimeOffset.TryParseExact(
                stripped, pattern, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fallback))
        {
            return fallback;
        }

        throw new InvalidDataException($"无法解析证书时间:{text}。");
    }

    private static (Asn1Element algorithm, Asn1Element keyBits)? FindSubjectPublicKeyInfo(List<Asn1Element> fields)
    {
        foreach (var field in fields)
        {
            if (!field.IsSequence)
            {
                continue;
            }

            var children = DerReader.ReadChildren(field);
            if (children.Count == 2 && children[0].IsSequence && children[1].Tag == 0x03)
            {
                return (children[0], children[1]);
            }
        }

        return null;
    }

    private static int ReadRsaKeyBits(Asn1Element bitString)
    {
        // BIT STRING: 首字节是 unused-bits 计数,其后是 RSAPublicKey SEQUENCE。
        var content = bitString.Content;
        if (content.Length < 2)
        {
            return 0;
        }

        var inner = content[1..];
        var offset = 0;
        var sequence = DerReader.Read(inner, ref offset, inner.Length);
        if (!sequence.IsSequence)
        {
            return 0;
        }

        var parts = DerReader.ReadChildren(sequence);
        return parts.Count >= 1 ? ReadPositiveIntegerBits(parts[0]) : 0;
    }

    private static int ReadPositiveIntegerBits(Asn1Element integer)
    {
        var span = integer.Content.Span;
        while (span.Length > 1 && span[0] == 0)
        {
            span = span[1..];
        }

        if (span.Length == 0)
        {
            return 0;
        }

        // 2048 位 RSA 的模数是 256 字节,用 int 累加会溢出,按位长度计算。
        var topByte = span[0];
        var topBits = 0;
        for (var v = topByte; v > 0; v >>= 1)
        {
            topBits++;
        }

        return ((span.Length - 1) * 8) + topBits;
    }

    private sealed record ExtensionFlags(bool CertificateAuthority, List<string> Ekus);

    private static ExtensionFlags ReadExtensions(List<Asn1Element> tbsFields) => ParseExtensions(tbsFields);

    private static ExtensionFlags ParseExtensions(List<Asn1Element> tbsFields)
    {
        var ekus = new List<string>();
        var isCa = false;

        var tagged = tbsFields.FirstOrDefault(f => f.IsContext(3));
        if (tagged.Raw.IsEmpty && tagged.ContentLength == 0)
        {
            return new ExtensionFlags(false, ekus);
        }

        var wrapper = DerReader.ReadChildren(tagged);
        if (wrapper.Count == 0)
        {
            return new ExtensionFlags(false, ekus);
        }

        var extensions = DerReader.ReadChildren(wrapper[0]);
        foreach (var extension in extensions)
        {
            var parts = DerReader.ReadChildren(extension);
            if (parts.Count < 2)
            {
                continue;
            }

            var oid = DerReader.ReadOid(parts[0]);
            // 最后一个元素是 extnValue OCTET STRING,里面再套一层 DER。
            var extnValue = parts[^1];
            if (extnValue.Tag != 0x04)
            {
                continue;
            }

            var inner = extnValue.Content;
            var offset = 0;
            var value = DerReader.Read(inner, ref offset, inner.Length);

            switch (oid)
            {
                case "2.5.29.15": // keyUsage
                    break;
                case "2.5.29.19": // basicConstraints
                    isCa = ReadBasicConstraintsCaf(value);
                    break;
                case "2.5.29.37": // extKeyUsage
                    foreach (var usage in DerReader.ReadChildren(value))
                    {
                        ekus.Add(DerReader.ReadOid(usage));
                    }

                    break;
            }
        }

        return new ExtensionFlags(isCa, ekus);
    }

    private static bool ReadBasicConstraintsCaf(Asn1Element value)
    {
        // BasicConstraints ::= SEQUENCE { cA BOOLEAN DEFAULT FALSE, pathLenConstraint INTEGER OPTIONAL }
        foreach (var child in DerReader.ReadChildren(value))
        {
            if (child.Tag == 0x01 && child.Content.Length == 1)
            {
                return child.Content.Span[0] != 0;
            }
        }

        return false;
    }
}
