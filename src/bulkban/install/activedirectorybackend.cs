using System.Globalization;

namespace bulkban.Install;

internal sealed class ActiveDirectoryBackend : IBackend
{
    private readonly string _activeDir;

    internal ActiveDirectoryBackend(string activeDir)
    {
        _activeDir = activeDir;
    }

    public string Name => "active";

    internal string ActiveDirectory => _activeDir;

    public bool Install(string cipPath, Guid policyId)
    {
        Directory.CreateDirectory(_activeDir);
        File.Copy(cipPath, CipPathFor(policyId), overwrite: true);
        return true;
    }

    public bool Remove(Guid policyId)
    {
        var path = CipPathFor(policyId);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public void Refresh()
    {
        // Active 目录写入后需要重启才生效,没有可立即执行的刷新动作。
    }

    internal string CipPathFor(Guid policyId) =>
        Path.Combine(_activeDir, CiToolBackend.FormatGuid(policyId) + ".cip");
}
