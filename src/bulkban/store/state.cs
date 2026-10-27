namespace bulkban.Store;

internal sealed record State(int SchemaVersion, IReadOnlyList<StateEntry> Policies)
{
    internal static State Empty { get; } = new(1, Array.Empty<StateEntry>());
}

internal sealed record StateEntry(
    string Name,
    string PolicyId,
    string Backend,
    string CipSha256,
    string InstalledAt,
    string Status);
