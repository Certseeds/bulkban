using bulkban.Processes;

namespace bulkban.Wdac;

internal sealed class PowerShellHost : IPowerShellHost
{
    private static readonly string PowerShellPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    public bool IsConfigCiAvailable()
    {
        var result = RunScript("if (Get-Command Add-SignerRule -ErrorAction SilentlyContinue) { 'yes' } else { 'no' }");
        return result.ExitCode == 0 && result.StandardOutput.Trim() == "yes";
    }

    public void AddDenySigner(string xmlPath, string certPath)
    {
        RunOrThrow($$"""
            Add-SignerRule -FilePath '{{Escape(xmlPath)}}' -CertificatePath '{{Escape(certPath)}}' -User -Deny -ErrorAction Stop
            """, "Add-SignerRule");
    }

    public void ConvertToCip(string xmlPath, string cipPath)
    {
        RunOrThrow($$"""
            ConvertFrom-CIPolicy -XmlFilePath '{{Escape(xmlPath)}}' -BinaryFilePath '{{Escape(cipPath)}}' -ErrorAction Stop
            """, "ConvertFrom-CIPolicy");
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static void RunOrThrow(string script, string operation)
    {
        var result = RunScript(script);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{operation} 失败(exit {result.ExitCode}):{result.StandardError.Trim()}");
        }
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunScript(string script)
    {
        var result = CommandRunner.Run(
            PowerShellPath,
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            script);

        return (result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
