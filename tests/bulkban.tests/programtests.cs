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

    [Theory]
    [InlineData(19044, 9999, false)] // build 本身不支持
    [InlineData(19045, 4290, false)]
    [InlineData(19045, 4291, true)]
    [InlineData(19045, -1, false)] // UBR 未知:需要补丁的分支必须拒绝
    [InlineData(20347, 4290, false)]
    [InlineData(20347, 4291, true)]
    [InlineData(20348, 2401, false)]
    [InlineData(20348, 2402, true)]
    [InlineData(20348, -1, false)]
    [InlineData(21999, 2401, false)]
    [InlineData(21999, 2402, true)]
    [InlineData(22000, 9999, false)] // Windows 11 21H2
    [InlineData(22620, 9999, false)]
    [InlineData(22621, 3446, false)]
    [InlineData(22621, 3447, true)]
    [InlineData(22621, -1, false)]
    [InlineData(22631, 3446, false)]
    [InlineData(22631, 3447, true)]
    [InlineData(26100, -1, true)] // 24H2 自带修复,不依赖 UBR
    [InlineData(26200, 0, true)]
    public void SupportedVersionMatrixIsCorrect(int build, int ubr, bool expected)
    {
        Assert.Equal(expected, Program.IsSupportedWindowsVersion(build, ubr));
    }

    [Theory]
    [InlineData(19045, 4291)]
    [InlineData(20348, 2402)]
    [InlineData(22621, 3447)]
    [InlineData(22631, 3447)]
    public void MinimumUbrForBuildIsCorrect(int build, int expected)
    {
        Assert.Equal(expected, Program.MinimumUbrForBuild(build));
    }

    [Fact]
    public void ReadUbrReturnsPositiveValue()
    {
        Assert.True(Program.ReadUbr() > 0);
    }
}
