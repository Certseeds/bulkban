using System.Security.Cryptography;
using System.Text;

namespace bulkban.Build;

internal static class SafeFileName
{
    private static readonly char[] Invalid = Path.GetInvalidFileNameChars();

    /// <summary>把组名转成安全的文件名;非法字符替换为 '_'。</summary>
    internal static string From(string groupName)
    {
        var builder = new StringBuilder(groupName.Length);
        foreach (var ch in groupName)
        {
            builder.Append(Array.IndexOf(Invalid, ch) >= 0 ? '_' : ch);
        }

        var result = builder.ToString().Trim();
        return result.Length == 0 ? "group" : result;
    }

    /// <summary>
    /// 清洗后的名字若与已用名冲突,追加原组名的短 hash,
    /// 避免两个组静默覆盖彼此的 XML/CIP。
    /// </summary>
    internal static string Unique(string groupName, HashSet<string> usedNames)
    {
        var candidate = From(groupName);
        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(groupName)))[..8];
        candidate = From(groupName) + "_" + suffix;

        if (!usedNames.Add(candidate))
        {
            throw new InvalidOperationException($"无法为组 '{groupName}' 生成唯一文件名。");
        }

        return candidate;
    }
}
