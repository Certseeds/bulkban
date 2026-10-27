using bulkban.Store;

namespace bulkban.Install;

internal interface IBackend
{
    string Name { get; }

    bool Install(string cipPath, Guid policyId);

    bool Remove(Guid policyId);

    void Refresh();
}

internal sealed record InstallPlan(IReadOnlyList<ManifestGroup> Groups, string BackendName);

internal sealed record InstallOutcome(IReadOnlyList<(string Group, bool Success, string Message)> Results)
{
    internal bool AllSucceeded => Results.All(r => r.Success);

    internal bool RequiresReboot => Results.Any(r => r.Message.Contains("重启", StringComparison.Ordinal));
}

internal sealed record RemoveOutcome(IReadOnlyList<(string Group, bool Success, string Message)> Results)
{
    internal bool AllSucceeded => Results.All(r => r.Success);
}
