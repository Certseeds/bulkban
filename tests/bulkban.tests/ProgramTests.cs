namespace bulkban.tests;

public class ProgramTests
{
    [Fact]
    public void ParseDryRunShouldSucceed()
    {
        var parseResult = Program.BuildRootCommand().Parse("--dry-run");

        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void ParseUnknownOptionShouldFail()
    {
        var parseResult = Program.BuildRootCommand().Parse("--nope");

        Assert.NotEmpty(parseResult.Errors);
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
