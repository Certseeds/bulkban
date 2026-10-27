using bulkban.Store;

namespace bulkban.Install;

internal static class Installer
{
    internal static InstallPlan Plan(Manifest manifest, string systemRoot, IBackend backend)
    {
        _ = systemRoot;
        return new InstallPlan(manifest.Groups, backend.Name);
    }

    /// <summary>逐组安装;单组失败不中断,结果写入 state。</summary>
    internal static InstallOutcome Execute(
        InstallPlan plan,
        string outputDir,
        IBackend backend,
        StateStore store,
        bool requireReboot)
    {
        var results = new List<(string Group, bool Success, string Message)>();

        foreach (var group in plan.Groups)
        {
            try
            {
                if (!Guid.TryParse(group.PolicyId, out var guid))
                {
                    results.Add((group.Name, false, $"manifest 中的 policyId 无效:{group.PolicyId}"));
                    continue;
                }

                var cip = Path.Combine(outputDir, group.Cip.Replace('/', Path.DirectorySeparatorChar));

                if (!backend.Install(cip, guid))
                {
                    results.Add((group.Name, false, $"{backend.Name} 返回失败"));
                    continue;
                }

                var status = requireReboot ? "pending-reboot" : "installed";
                store.Upsert(new StateEntry(
                    group.Name,
                    group.PolicyId,
                    backend.Name,
                    Store.Hashing.Sha256File(cip),
                    DateTimeOffset.UtcNow.ToString("O"),
                    status));

                var message = requireReboot ? "已写入,需要重启后生效" : "已安装";
                results.Add((group.Name, true, message));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or FormatException)
            {
                results.Add((group.Name, false, ex.Message));
            }
        }

        if (results.Any(r => r.Success))
        {
            backend.Refresh();
        }

        return new InstallOutcome(results);
    }

    /// <summary>按 state 卸载;not found 视为警告并继续。</summary>
    internal static RemoveOutcome RemoveAll(State state, IBackend backend, StateStore store)
    {
        var results = new List<(string Group, bool Success, string Message)>();

        foreach (var entry in state.Policies)
        {
            try
            {
                if (!Guid.TryParse(entry.PolicyId, out var guid))
                {
                    results.Add((entry.Name, false, $"state.json 中的 policyId 无效:{entry.PolicyId}"));
                    continue;
                }

                if (backend.Remove(guid))
                {
                    store.Remove(entry.PolicyId);
                    results.Add((entry.Name, true, "已移除"));
                }
                else
                {
                    results.Add((entry.Name, true, "未找到(可能已移除)"));
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or FormatException)
            {
                results.Add((entry.Name, false, ex.Message));
            }
        }

        backend.Refresh();
        return new RemoveOutcome(results);
    }
}
