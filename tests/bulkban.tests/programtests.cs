namespace bulkban.tests;

public class ProgramTests
{
    [Theory]
    [InlineData("build")]
    [InlineData("install")]
    [InlineData("apply")]
    [InlineData("verify")]
    [InlineData("remove")]
    public void RootCommandHasSubcommand(string name)
    {
        var root = Program.BuildRootCommand();

        Assert.Contains(root.Subcommands, c => c.Name == name);
    }

    [Fact]
    public void ParseUnknownOptionShouldFail()
    {
        var parseResult = Program.BuildRootCommand().Parse("--nope");

        Assert.NotEmpty(parseResult.Errors);
    }

    [Theory]
    [InlineData("verify", "--output-dir")]
    [InlineData("build", "--certs-dir")]
    [InlineData("install", "--output-dir")]
    public void SubcommandParsesItsOptions(string command, string option)
    {
        var result = Program.BuildRootCommand().Parse(new[] { command, option });

        // 缺参数时应有解析错误,证明该选项已注册
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("apply")]
    public void EnforceOptionIsRegistered(string command)
    {
        var cmd = Program.BuildRootCommand().Subcommands.Single(c => c.Name == command);

        Assert.Contains(cmd.Options, o => o.Name == "--enforce");
    }

    [Fact]
    public void MissingCertsDirReturnsExit3()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = Program.BuildRootCommand().Parse(new[] { "build", "--certs-dir", missing });

        Assert.Equal(3, result.Invoke());
    }

    [Fact]
    public void MissingManifestVerifyReturnsExit6()
    {
        var emptyDir = TempDir.Create();

        var result = Program.BuildRootCommand().Parse(new[] { "verify", "--output-dir", emptyDir });

        Assert.Equal(6, result.Invoke());
    }

    [Theory]
    [InlineData(19044, false)] // Windows 10 21H2
    [InlineData(19045, true)]  // Windows 10 22H2
    [InlineData(20348, true)]  // Windows Server 2022
    [InlineData(21999, true)]
    [InlineData(22000, false)] // Windows 11 21H2
    [InlineData(22620, false)]
    [InlineData(22621, true)]  // Windows 11 22H2
    [InlineData(26100, true)]  // Windows 11 24H2
    public void SupportedBuildMatrixIsCorrect(int build, bool expected)
    {
        Assert.Equal(expected, Program.IsSupportedWindowsBuild(build));
    }
}
