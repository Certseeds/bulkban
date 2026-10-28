namespace bulkban.Wdac;

/// <summary>Audit = 只记录/告警;Enforce = 真正阻止运行(ban)。</summary>
internal enum PolicyMode
{
    Audit,
    Enforce,
}

internal static class PolicyModeExtensions
{
    internal static string ToManifestValue(this PolicyMode mode) =>
        mode == PolicyMode.Audit ? "audit" : "enforce";
}
