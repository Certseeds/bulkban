using bulkban.Build;
using bulkban.Install;
using bulkban.Store;
using bulkban.tests.Support;
using bulkban.Wdac;

namespace bulkban.tests.Install;

public class VerifierTests
{
    [Fact]
    public void TamperedCipIsReported()
    {
        var root = TempDir.BuiltOutput();

        File.AppendAllText(Path.Combine(root, "audit-cips", "a.cip"), "x");

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
    public void CipInWrongModeDirectoryIsReported()
    {
        var root = TempDir.BuiltOutput(PolicyMode.Enforce);
        var manifest = TempDir.LoadManifest(root);
        TempDir.RewriteManifest(root, manifest with { Mode = "audit" });

        var errors = Verifier.Verify(root);

        Assert.Contains(errors, e => e.Contains("audit-cips/", StringComparison.Ordinal));
    }

    [Fact]
    public void CipContentModeMismatchIsReported()
    {
        var root = TempDir.BuiltOutput(PolicyMode.Audit);
        Directory.CreateDirectory(Path.Combine(root, "enforce-cips"));
        File.Copy(
            Path.Combine(root, "audit-cips", "a.cip"),
            Path.Combine(root, "enforce-cips", "a.cip"));

        var manifest = TempDir.LoadManifest(root);
        var group = manifest.Groups[0] with { Cip = "enforce-cips/a.cip" };
        TempDir.RewriteManifest(
            root,
            manifest with { Mode = "enforce", Groups = new[] { group } });

        var errors = Verifier.Verify(root);

        Assert.Contains(errors, e => e.Contains("模式不匹配", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingArtifactIsReported()
    {
        var root = TempDir.BuiltOutput();
        File.Delete(Path.Combine(root, "audit-xmls", "a.xml"));

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
