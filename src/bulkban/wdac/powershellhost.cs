using bulkban.Processes;

namespace bulkban.Wdac;

internal sealed class PowerShellHost : IPowerShellHost
{
    private const string Utf8OutputPrelude =
        "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8; ";

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
            """, "Add-SignerRule", certPath);
    }

    public void ConvertToCip(string xmlPath, string cipPath)
    {
        RunOrThrow($$"""
            ConvertFrom-CIPolicy -XmlFilePath '{{Escape(xmlPath)}}' -BinaryFilePath '{{Escape(cipPath)}}' -ErrorAction Stop
            """, "ConvertFrom-CIPolicy", xmlPath);
    }

    /// <summary>
    /// 子进程由 CliWrap 按 UTF-8 解码;Windows PowerShell 5.1 默认按控制台代码页
    /// 输出中文,不强制 UTF-8 时错误信息会变成问号。
    /// </summary>
    internal static string WrapScript(string script) => Utf8OutputPrelude + script;

    internal static string FormatFailure(string operation, int exitCode, string context, string standardError)
    {
        var detail = string.IsNullOrWhiteSpace(standardError) ? string.Empty : standardError.Trim();
        return $"{operation} 失败(exit {exitCode}) [{context}]:{detail}";
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static void RunOrThrow(string script, string operation, string context)
    {
        var result = RunScript(script);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                FormatFailure(operation, result.ExitCode, context, result.StandardError));
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
            WrapScript(script));

        return (result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
