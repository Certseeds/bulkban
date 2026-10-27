using System.Diagnostics;
using System.Globalization;

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
            return Run($"--update-policy \"{staged}\"", out var error) is { } ok && ok
                ? true
                : ThrowWithMessage(error);
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
        return Run($"--remove-policy \"{FormatGuid(policyId)}\"", out _);
    }

    public void Refresh()
    {
        Run("--refresh", out _);
    }

    internal static string FormatGuid(Guid id) =>
        "{" + id.ToString().ToUpperInvariant() + "}";

    private bool Run(string arguments, out string error)
    {
        var startInfo = new ProcessStartInfo(_ciToolPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = arguments + " --json",
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            error = "无法启动 CiTool.exe";
            return false;
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return process.ExitCode == 0;
    }

    private static bool ThrowWithMessage(string error)
    {
        throw new InvalidOperationException($"CiTool 安装失败:{error.Trim()}");
    }
}
