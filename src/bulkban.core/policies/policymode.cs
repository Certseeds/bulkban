namespace bulkban.Core.Policies;

/// <summary>Audit = 只记录/告警;Enforce = 真正阻止运行(ban)。</summary>
internal enum PolicyMode
{
    Audit,
    Enforce,
}
