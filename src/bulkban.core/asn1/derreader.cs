using System.Buffers.Binary;
using System.Text;

namespace bulkban.Core.Asn1;

/// <summary>一个已解析的 DER TLV。只保留定位信息,内容按需切片。</summary>
internal readonly struct Asn1Element
{
    private readonly ReadOnlyMemory<byte> _data;

    internal Asn1Element(ReadOnlyMemory<byte> data, byte tag, int rawOffset, int contentOffset, int contentLength)
    {
        _data = data;
        Tag = tag;
        RawOffset = rawOffset;
        ContentOffset = contentOffset;
        ContentLength = contentLength;
    }

    internal byte Tag { get; }

    /// <summary>原始缓冲区,供子元素解析时复用。</summary>
    internal ReadOnlyMemory<byte> Backing => _data;

    /// <summary>TLV 起点(含 tag)。</summary>
    internal int RawOffset { get; }

    internal int ContentOffset { get; }

    internal int ContentLength { get; }

    internal ReadOnlyMemory<byte> Content => _data.Slice(ContentOffset, ContentLength);

    /// <summary>含 tag 与 length 的完整编码。WDAC 的 TBS 哈希就是对这段求值。</summary>
    internal ReadOnlyMemory<byte> Raw => _data[RawOffset..(ContentOffset + ContentLength)];

    internal int ContentEnd => ContentOffset + ContentLength;

    internal bool IsSequence => Tag == 0x30;

    internal bool IsContext(int index) => Tag == (0xA0 + index);

    internal string? AsString() => Tag switch
    {
        0x0C => Encoding.UTF8.GetString(Content.Span),          // UTF8String
        0x1E => Encoding.BigEndianUnicode.GetString(Content.Span), // BMPString
        0x13 or 0x16 => Encoding.ASCII.GetString(Content.Span), // PrintableString / IA5String
        _ => null,
    };
}

/// <summary>
/// 极简 DER 解析器。只覆盖解析 X.509 证书所需的部分:逐层读 TLV、不做 schema 校验。
/// 不依赖 <c>System.Security.Cryptography.X509Certificates</c>,因此能在浏览器里跑。
/// </summary>
internal static class DerReader
{
    /// <summary>读取 <paramref name="offset"/> 处的 TLV 头部。</summary>
    internal static bool TryReadHeader(
        ReadOnlyMemory<byte> data, int offset, int limit, out int contentOffset, out int contentLength, out byte tag)
    {
        contentOffset = 0;
        contentLength = 0;
        tag = 0;

        if (offset < 0 || offset >= limit)
        {
            return false;
        }

        var span = data.Span;
        tag = span[offset];
        var cursor = offset + 1;
        if (cursor >= limit)
        {
            return false;
        }

        var first = span[cursor++];
        int length;
        if ((first & 0x80) == 0)
        {
            length = first;
        }
        else
        {
            var count = first & 0x7F;
            // 不接受不定长(0x80):X.509 证书都是确定长度。
            if (count == 0 || count > 4 || cursor + count > limit)
            {
                return false;
            }

            length = count switch
            {
                1 => span[cursor],
                2 => BinaryPrimitives.ReadUInt16BigEndian(span[cursor..]),
                3 => (span[cursor] << 16) | BinaryPrimitives.ReadUInt16BigEndian(span[(cursor + 1)..]),
                _ => (int)BinaryPrimitives.ReadUInt32BigEndian(span[cursor..]),
            };
            cursor += count;
        }

        if (length < 0 || cursor + length > limit)
        {
            return false;
        }

        contentOffset = cursor;
        contentLength = length;
        return true;
    }

    /// <summary>读取 <paramref name="offset"/> 处的 TLV 并前移 <paramref name="offset"/>。</summary>
    internal static Asn1Element Read(ReadOnlyMemory<byte> data, ref int offset, int limit)
    {
        var start = offset;
        if (!TryReadHeader(data, offset, limit, out var contentOffset, out var contentLength, out var tag))
        {
            throw new InvalidDataException($"DER 头部损坏:偏移 {offset}。");
        }

        offset = contentOffset + contentLength;
        return new Asn1Element(data, tag, start, contentOffset, contentLength);
    }

    /// <summary>读出构造类型(SEQUENCE / SET / [n])的全部子元素。</summary>
    internal static List<Asn1Element> ReadChildren(Asn1Element parent)
    {
        var children = new List<Asn1Element>();
        var offset = parent.ContentOffset;
        var limit = parent.ContentEnd;
        while (offset < limit)
        {
            children.Add(Read(parent.Backing, ref offset, limit));
        }

        return children;
    }

    /// <summary>读取 OID 的点分十进制表示。</summary>
    internal static string ReadOid(Asn1Element element)
    {
        var content = element.Content.Span;
        if (content.Length == 0)
        {
            return string.Empty;
        }

        var parts = new List<uint>();
        uint value = 0;
        var pending = false;
        foreach (var b in content)
        {
            value = (value << 7) | (uint)(b & 0x7F);
            pending = true;
            if ((b & 0x80) == 0)
            {
                parts.Add(value);
                value = 0;
                pending = false;
            }
        }

        if (pending || parts.Count == 0)
        {
            return string.Empty;
        }

        // 第一段是压缩编码:first*40 + second
        var builder = new StringBuilder();
        builder.Append(parts[0] / 40).Append('.').Append(parts[0] % 40);
        for (var i = 1; i < parts.Count; i++)
        {
            builder.Append('.').Append(parts[i]);
        }

        return builder.ToString();
    }
}
