using bulkban.Paths;

namespace bulkban.tests.Paths;

public class PathResolverTests
{
    [Fact]
    public void CertsDirPrefersOption()
    {
        var (root, baseDir, currentDir) = TempDir.PathsWithCerts();

        Assert.Equal(
            Path.Combine(root, "explicit"),
            PathResolver.ResolveCertsDir(Path.Combine(root, "explicit"), baseDir, currentDir));
    }

    [Fact]
    public void CertsDirFallsBackToBaseThenCurrent()
    {
        var (root, baseDir, currentDir) = TempDir.PathsWithCerts();
        _ = root;
        _ = currentDir;

        Assert.Equal(Path.Combine(baseDir, "certs"), PathResolver.ResolveCertsDir(null, baseDir, currentDir));
    }

    [Fact]
    public void CertsDirFallsBackToCurrentDirWhenBaseHasNone()
    {
        var root = TempDir.Create();
        var emptyBase = Path.Combine(root, "app");
        var cwd = Path.Combine(root, "work");
        Directory.CreateDirectory(emptyBase);
        Directory.CreateDirectory(Path.Combine(cwd, "certs"));

        Assert.Equal(Path.Combine(cwd, "certs"), PathResolver.ResolveCertsDir(null, emptyBase, cwd));
    }

    [Fact]
    public void CertsDirThrowsWhenMissing()
    {
        var root = TempDir.Create();

        Assert.Throws<DirectoryNotFoundException>(() => PathResolver.ResolveCertsDir(null, root, root));
    }

    [Fact]
    public void OutputDirDefaultsToParentOfCerts()
    {
        var root = TempDir.Create();
        var certs = Path.Combine(root, "certs");

        Assert.Equal(root, PathResolver.ResolveOutputDir(null, certs, root));
    }

    [Fact]
    public void OutputDirHonorsOption()
    {
        var root = TempDir.Create();

        Assert.Equal(Path.Combine(root, "out"), PathResolver.ResolveOutputDir(Path.Combine(root, "out"), Path.Combine(root, "certs"), root));
    }
}
