namespace bulkban.Install;

internal static class BackendSelector
{
    /// <summary>
    /// 有 CiTool.exe 用 CiTool;否则回退到 CodeIntegrity\CiPolicies\Active。
    /// systemRoot 参数便于测试;生产环境传入 %SystemRoot%。
    /// </summary>
    internal static IBackend? Select(string systemRoot)
    {
        var system32 = Path.Combine(systemRoot, "System32");
        var ciToolPath = Path.Combine(system32, "CiTool.exe");
        if (File.Exists(ciToolPath))
        {
            return new CiToolBackend(ciToolPath);
        }

        var activeDir = Path.Combine(system32, "CodeIntegrity", "CiPolicies", "Active");
        if (Directory.Exists(activeDir))
        {
            return new ActiveDirectoryBackend(activeDir);
        }

        return null;
    }
}
