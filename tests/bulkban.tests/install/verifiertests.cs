using bulkban.Build;
using bulkban.Install;
using bulkban.tests.Support;

namespace bulkban.tests.Install;

public class VerifierTests
{
    [Fact]
    public void TamperedCipIsReported()
    {
        var root = TempDir.BuiltOutput();

        File.AppendAllText(Path.Combine(root, "cips", "a.cip"), "x");

        var errors = Verifier.Verify(root);

        Assert.Single(errors);
        Assert.Contains("a.cip", errors[0]);
    }

    [Fact]
    public void IntactOutputPasses()
    {
        var root = TempDir.BuiltOutput();

        Assert.Empty(Verifier.Verify(root));
    }

    [Fact]
    public void MissingArtifactIsReported()
    {
        var root = TempDir.BuiltOutput();
        File.Delete(Path.Combine(root, "xmls", "a.xml"));

        var errors = Verifier.Verify(root);

        Assert.Single(errors);
        Assert.Contains("a.xml", errors[0]);
    }

    [Fact]
    public void MissingManifestIsReported()
    {
        var root = TempDir.Create();

        var errors = Verifier.Verify(root);

        Assert.Single(errors);
        Assert.Contains("manifest.json", errors[0]);
    }
}
