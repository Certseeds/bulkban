namespace bulkban.Wdac;

/// <summary>为每个证书组派生稳定的 WDAC PolicyID。</summary>
internal static class PolicyId
{
    /// <summary>
    /// UUIDv5(URL namespace, "https://github.com/Certseeds/bulkban")。
    /// 一次性固定,不要改动:改动会让所有已安装策略变成孤儿。
    /// </summary>
    internal static readonly Guid ProjectNamespace = Guid.Parse("f33008cd-8a06-57af-ac45-7be44e913afc");

    internal static Guid ForGroup(string group)
    {
        return UuidV5.Create(ProjectNamespace, "v1/" + group.ToLowerInvariant());
    }
}
