namespace bulkban.Core.Policies;

internal static class PolicyModeExtensions
{
    internal static string ToManifestValue(this PolicyMode mode) =>
        mode == PolicyMode.Audit ? "audit" : "enforce";

    /// <summary>该模式的 XML 中间产物目录,例如 audit-xmls。</summary>
    internal static string XmlDirectory(this PolicyMode mode) =>
        mode.ToManifestValue() + "-xmls";

    /// <summary>该模式的 CIP 产物目录,例如 audit-cips。</summary>
    internal static string CipDirectory(this PolicyMode mode) =>
        mode.ToManifestValue() + "-cips";
}
