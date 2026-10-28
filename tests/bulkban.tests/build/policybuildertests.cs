using bulkban.Build;
using bulkban.tests.Support;

namespace bulkban.tests.Build;

public class PolicyBuilderTests
{
    private static readonly string[] SubdirAAndOthers = { "a", "others" };
    private static readonly string[] SubdirAAndB = { "a", "b" };
    [Fact]
    public void FailedBuildKeepsPreviousOutputs()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        var options = new BuildOptions(Path.Combine(root, "certs"), root);
        PolicyBuilder.Build(options, new FakePowerShellHost());

        var manifestBefore = File.ReadAllText(Path.Combine(root, "manifest.json"));
        TempDir.WriteCorruptCer(root, "certs/a/bad.cer");

        Assert.Throws<InvalidOperationException>(() => PolicyBuilder.Build(options, new FakePowerShellHost()));
        Assert.Equal(manifestBefore, File.ReadAllText(Path.Combine(root, "manifest.json")));
    }

    [Fact]
    public void BuildWritesXmlCipHashAndManifest()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        TempDir.WriteCer(root, "certs/2.cer");

        var result = PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        Assert.Equal(SubdirAAndOthers, result.Manifest.Groups.Select(g => g.Name));
        Assert.True(File.Exists(Path.Combine(root, "audit-xmls", "a.xml")));
        Assert.True(File.Exists(Path.Combine(root, "audit-cips", "a.cip")));
        Assert.True(File.Exists(Path.Combine(root, "audit-xmls", "a.xml.sha256")));
        Assert.True(File.Exists(Path.Combine(root, "audit-cips", "a.cip.sha256")));
        Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
    }

    [Fact]
    public void UnsupportedCertIsRecordedNotFatal()
    {
        var root = TempDir.Create();
        TempDir.WriteEcdsaCer(root, "certs/ecc.cer");
        TempDir.WriteCer(root, "certs/rsa.cer");

        var result = PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        var group = Assert.Single(result.Manifest.Groups);
        Assert.Equal(1, group.CertCount);
        Assert.Single(group.Unsupported);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void CipContainsRealPolicyIdNotTemplatePlaceholder()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        var host = new bulkban.Wdac.PowerShellHost();
        if (!host.IsConfigCiAvailable())
        {
            return;
        }

        var result = PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), host);
        var group = Assert.Single(result.Manifest.Groups);
        var cip = File.ReadAllBytes(Path.Combine(root, "audit-cips", "a.cip"));

        var realId = Guid.Parse(group.PolicyId);
        var templateId = Guid.Parse("31351756-3F24-4963-8380-4E7602335AAE");

        Assert.Equal(3, CountOccurrences(cip, realId.ToByteArray(bigEndian: false)));
        Assert.Equal(0, CountOccurrences(cip, templateId.ToByteArray(bigEndian: false)));
    }

    [Fact]
    public void CipReflectsPrunedRuleOptions()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");

        PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        var xml = File.ReadAllText(Path.Combine(root, "audit-xmls", "a.xml"));
        Assert.DoesNotContain("Inherit Default Policy", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("Revoked Expired As Unsigned", xml, StringComparison.Ordinal);
    }

    private static int CountOccurrences(byte[] haystack, byte[] needle)
    {
        var count = 0;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void BuildDefaultsToAuditMode()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");

        var result = PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        Assert.Equal("audit", result.Manifest.Mode);
        Assert.Contains("Enabled:Audit Mode", File.ReadAllText(Path.Combine(root, "audit-xmls", "a.xml")));
    }

    [Fact]
    public void BuildEnforceModeOmitsAuditRule()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");

        var result = PolicyBuilder.Build(
            new BuildOptions(Path.Combine(root, "certs"), root, bulkban.Wdac.PolicyMode.Enforce),
            new FakePowerShellHost());

        Assert.Equal("enforce", result.Manifest.Mode);
        Assert.DoesNotContain("Enabled:Audit Mode", File.ReadAllText(Path.Combine(root, "enforce-xmls", "a.xml")));
    }

    [Fact]
    public void BuildEnforceKeepsAuditCip()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        var options = new BuildOptions(Path.Combine(root, "certs"), root);
        PolicyBuilder.Build(options, new FakePowerShellHost());
        var auditCip = File.ReadAllBytes(Path.Combine(root, "audit-cips", "a.cip"));

        var result = PolicyBuilder.Build(
            options with { Mode = bulkban.Wdac.PolicyMode.Enforce },
            new FakePowerShellHost());

        Assert.Equal("enforce", result.Manifest.Mode);
        Assert.True(File.Exists(Path.Combine(root, "enforce-cips", "a.cip")));
        Assert.Equal(auditCip, File.ReadAllBytes(Path.Combine(root, "audit-cips", "a.cip")));
    }

    [Fact]
    public void GroupWithOnlyUnsupportedCertsProducesNoPolicy()
    {
        var root = TempDir.Create();
        TempDir.WriteEcdsaCer(root, "certs/ecc-only/1.cer");
        TempDir.WriteCer(root, "certs/rsa/1.cer");

        var result = PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        Assert.Single(result.Manifest.Groups);
        Assert.Equal("rsa", result.Manifest.Groups[0].Name);
        Assert.DoesNotContain(Directory.GetFiles(Path.Combine(root, "audit-cips")), f => f.Contains("ecc", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("ecc-only", StringComparison.Ordinal));
    }

    [Fact]
    public void SanitizedFileNameCollisionsGetUniqueSuffix()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var first = SafeFileName.Unique("a<b", used);
        var second = SafeFileName.Unique("a>b", used);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RebuildReplacesAllThreeArtifactsTogether()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        var options = new BuildOptions(Path.Combine(root, "certs"), root);
        PolicyBuilder.Build(options, new FakePowerShellHost());

        TempDir.WriteCer(root, "certs/b/1.cer");
        var result = PolicyBuilder.Build(options, new FakePowerShellHost());

        Assert.Equal(SubdirAAndB, result.Manifest.Groups.Select(g => g.Name));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "audit-xmls")).Count(f => f.EndsWith(".xml", StringComparison.Ordinal)));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "audit-cips")).Count(f => f.EndsWith(".cip", StringComparison.Ordinal)));
        Assert.Empty(Directory.GetDirectories(root, ".backup-*"));
    }

    [Fact]
    public void InterruptedCommitBackupIsRecoveredOnNextBuild()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");
        PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        // 模拟上次提交中断:audit-xmls/ 被移进备份后进程死亡。
        var backup = Path.Combine(root, ".backup-deadbeef");
        Directory.CreateDirectory(backup);
        Directory.Move(Path.Combine(root, "audit-xmls"), Path.Combine(backup, "audit-xmls"));

        PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        Assert.True(File.Exists(Path.Combine(root, "audit-xmls", "a.xml")));
        Assert.Empty(Directory.GetDirectories(root, ".backup-*"));
    }

    [Fact]
    public void BuildStagingDirectoryIsRemoved()
    {
        var root = TempDir.Create();
        TempDir.WriteCer(root, "certs/a/1.cer");

        PolicyBuilder.Build(new BuildOptions(Path.Combine(root, "certs"), root), new FakePowerShellHost());

        Assert.Empty(Directory.GetDirectories(root, ".staging-*"));
    }
}
