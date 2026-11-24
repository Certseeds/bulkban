using System.IO;
using Microsoft.Security.CodeIntegrity;

namespace bulkban.Web.Cip;

/// <summary>
/// 浏览器端的 XML → CIP。走 <c>Microsoft.Security.CodeIntegrity</c>,
/// 与 Windows 侧的 ConfigCI 是同一套实现,因此不需要 WASM 里调用任何系统组件。
/// </summary>
internal static class CodeIntegrityConverter
{
    internal static byte[] Convert(string policyXml)
    {
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(policyXml));
        using var output = new MemoryStream();
        Policy.ConvertFromCodeIntegrityPolicy(input, output);
        return output.ToArray();
    }
}
