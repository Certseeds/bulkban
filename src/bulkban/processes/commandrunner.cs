using System.ComponentModel;
using System.Text;
using CliWrap;
using CliWrap.Buffered;

namespace bulkban.Processes;

/// <summary>子进程执行结果;单条输出超过上限时会被截断。</summary>
internal sealed record CommandOutput(int ExitCode, string StandardOutput, string StandardError);

internal static class CommandRunner
{
    /// <summary>stdout/stderr 各自保留的最大字符数,避免把整段输出拼进异常信息。</summary>
    internal const int MaxCapturedLength = 8 * 1024;

    private const string TruncatedSuffix = "...(truncated)";

    /// <summary>
    /// 用 CliWrap 执行命令:stdout 和 stderr 会被并发读取,不会出现
    /// "先读 stdout、stderr 写满管道缓冲区后互等" 的死锁。
    /// </summary>
    internal static CommandOutput Run(string fileName, params string[] arguments)
    {
        try
        {
            var result = Cli.Wrap(fileName)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(Encoding.UTF8)
                .GetAwaiter()
                .GetResult();

            return new CommandOutput(
                result.ExitCode,
                Truncate(result.StandardOutput),
                Truncate(result.StandardError));
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"无法启动 {fileName}:{ex.Message}", ex);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxCapturedLength
            ? value
            : string.Concat(value.AsSpan(0, MaxCapturedLength), TruncatedSuffix);
}
