using bulkban.Processes;

namespace bulkban.tests.Processes;

public class CommandRunnerTests
{
    private const string LongLine = "0123456789012345678901234567890123456789";

    [Fact]
    public void CapturesExitCodeAndBothStreams()
    {
        var result = CommandRunner.Run("cmd.exe", "/c", "echo out & echo err 1>&2 & exit 3");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("out", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("err", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LargeStderrDoesNotBlock()
    {
        // 旧实现先 ReadToEnd(stdout) 再读 stderr:stderr 写满管道缓冲区后子进程阻塞,
        // 父进程又在等 stdout 结束,两边互等。这里 5000 行(约 200 KB)足以触发。
        var run = Task.Run(() => CommandRunner.Run(
            "cmd.exe",
            "/c",
            "for /L %i in (1,1,5000) do @echo " + LongLine + " 1>&2"));

        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.Same(run, finished);
        var result = await run;
        Assert.True(result.StandardError.Length <= CommandRunner.MaxCapturedLength + 64);
    }

    [Fact]
    public void TruncatesOversizedStdout()
    {
        var result = CommandRunner.Run(
            "cmd.exe",
            "/c",
            "for /L %i in (1,1,2000) do @echo " + LongLine);

        Assert.True(result.StandardOutput.Length <= CommandRunner.MaxCapturedLength + 64);
        Assert.EndsWith("(truncated)", result.StandardOutput, StringComparison.Ordinal);
        Assert.True(result.StandardError.Length <= CommandRunner.MaxCapturedLength + 64);
    }
}
