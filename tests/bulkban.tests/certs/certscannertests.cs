using bulkban.Certs;

namespace bulkban.tests.Certs;

public class CertScannerTests
{
    private static readonly string[] TwoSubdirsAndOthers = { "a", "b", "others" };
    private static readonly string[] OnlySubdirA = { "a" };
    private static readonly string[] SubdirAAndOthers = { "a", "others" };
    private static readonly string[] MergedOthersFiles = { "2.cer", "3.cer" };

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
    public void OthersSubdirectoryIsMergedIntoOthersGroup()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        TempDir.WriteCer(root, "certs/2.cer");
        TempDir.WriteCer(root, "certs/others/3.cer");

        var groups = CertScanner.Scan(Path.Combine(root, "certs"));

        Assert.Equal(SubdirAAndOthers, groups.Select(g => g.Name));
        var others = groups.Single(g => g.Name == "others");
        Assert.Equal(MergedOthersFiles, others.Files.Select(f => f.FileName));
    }

    [Fact]
    public void OthersSubdirectoryAloneBecomesOthersGroup()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/others/1.cer");

        var groups = CertScanner.Scan(Path.Combine(root, "certs"));

        var group = Assert.Single(groups);
        Assert.Equal("others", group.Name);
        Assert.Equal("1.cer", Assert.Single(group.Files).FileName);
    }

    [Fact]
    public void OthersSubdirectoryWithNonCerFileIsRejected()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/others/1.cer");
        File.WriteAllText(Path.Combine(root, "certs", "others", "readme.txt"), "not a certificate");

        var ex = Assert.Throws<InvalidOperationException>(() => CertScanner.Scan(Path.Combine(root, "certs")));

        Assert.Contains("others", ex.Message, StringComparison.Ordinal);
        Assert.Contains("readme.txt", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OthersSubdirectoryWithNestedDirectoryIsRejected()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/others/1.cer");
        Directory.CreateDirectory(Path.Combine(root, "certs", "others", "nested"));

        var ex = Assert.Throws<InvalidOperationException>(() => CertScanner.Scan(Path.Combine(root, "certs")));

        Assert.Contains("nested", ex.Message, StringComparison.Ordinal);
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
