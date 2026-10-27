namespace bulkban.tests;

public class PublishGuardTests
{
    [Fact]
    public void PublishScriptRejectsCertsDirectory()
    {
        var script = File.ReadAllText(Path.Combine(TempDir.RepoRoot(), "scripts", "publish.ps1"));

        Assert.Contains("certs", script);
        Assert.Contains(".cer", script);
    }
}
