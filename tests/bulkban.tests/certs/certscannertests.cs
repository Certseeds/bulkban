using bulkban.Certs;

namespace bulkban.tests.Certs;

public class CertScannerTests
{
    private static readonly string[] TwoSubdirsAndOthers = { "a", "b", "others" };
    private static readonly string[] OnlySubdirA = { "a" };

    [Fact]
    public void SubdirectoriesBecomeGroupsAndTopLevelBecomesOthers()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        TempDir.WriteCer(root, "certs/b/2.cer");
        TempDir.WriteCer(root, "certs/3.cer");

        var groups = CertScanner.Scan(Path.Combine(root, "certs"));

        Assert.Equal(TwoSubdirsAndOthers, groups.Select(g => g.Name));
        Assert.Equal(3, groups.Sum(g => g.Files.Count));
    }

    [Fact]
    public void NestedDirectoriesInsideGroupAreIgnored()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        TempDir.WriteCer(root, "certs/a/nested/2.cer");

        var groups = CertScanner.Scan(Path.Combine(root, "certs"));

        Assert.Single(groups);
        Assert.Single(groups[0].Files);
    }

    [Fact]
    public void SubdirectoryNamedOthersIsRejected()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/others/1.cer");

        var ex = Assert.Throws<InvalidOperationException>(() => CertScanner.Scan(Path.Combine(root, "certs")));
        Assert.Contains("others", ex.Message);
    }

    [Fact]
    public void EmptyGroupIsSkipped()
    {
        var root = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(root, "certs", "empty"));
        TempDir.WriteCer(root, "certs/a/1.cer");

        var groups = CertScanner.Scan(Path.Combine(root, "certs"));

        Assert.Equal(OnlySubdirA, groups.Select(g => g.Name));
    }
}
