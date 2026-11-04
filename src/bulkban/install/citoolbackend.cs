using bulkban.Processes;

namespace bulkban.Install;

internal sealed class CiToolBackend : IBackend
{
    private readonly string _ciToolPath;

    internal CiToolBackend(string ciToolPath)
    {
        _ciToolPath = ciToolPath;
    }

    public string Name => "citool";

    public bool Install(string cipPath, Guid policyId)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bulkban");
        Directory.CreateDirectory(tempDir);
        var staged = Path.Combine(tempDir, FormatGuid(policyId) + ".cip");

        try
        {
            File.Copy(cipPath, staged, overwrite: true);
            if (Run(out var error, "--update-policy", staged, "--json"))
            {
                return true;
            }

            throw new InvalidOperationException($"CiTool 安装失败:{error.Trim()}");
        }
        finally
        {
            if (File.Exists(staged))
            {
                File.Delete(staged);
            }
        }
    }

    public bool Remove(Guid policyId)
    {
        return Run(out _, "--remove-policy", FormatGuid(policyId), "--json");
    }

    public void Refresh()
    {
        Run(out _, "--refresh", "--json");
    }

    internal static string FormatGuid(Guid id) =>
        "{" + id.ToString().ToUpperInvariant() + "}";

    private bool Run(out string error, params string[] arguments)
    {
        CommandOutput result;
        try
        {
            result = CommandRunner.Run(_ciToolPath, arguments);
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }

        error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return result.ExitCode == 0;
    }
}
