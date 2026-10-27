using System.Text.Json;
using bulkban.Store;

namespace bulkban.Install;

internal sealed class StateStore
{
    private readonly string _path;

    internal StateStore(string outputDir)
    {
        _path = Path.Combine(outputDir, "state.json");
    }

    internal State Load()
    {
        if (!File.Exists(_path))
        {
            return State.Empty;
        }

        var state = JsonSerializer.Deserialize(File.ReadAllText(_path), JsonContext.Default.State);
        return state ?? State.Empty;
    }

    internal void Save(State state)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(state, JsonContext.Default.State));
    }

    internal void Upsert(StateEntry entry)
    {
        var state = Load();
        var entries = state.Policies.ToList();
        entries.RemoveAll(e => string.Equals(e.PolicyId, entry.PolicyId, StringComparison.OrdinalIgnoreCase));
        entries.Add(entry);
        Save(new State(state.SchemaVersion, entries));
    }

    internal void Remove(string policyId)
    {
        var state = Load();
        var entries = state.Policies
            .Where(e => !string.Equals(e.PolicyId, policyId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Save(new State(state.SchemaVersion, entries));
    }
}
