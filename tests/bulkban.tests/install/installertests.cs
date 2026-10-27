using bulkban.Install;
using bulkban.Store;

namespace bulkban.tests.Install;

public class InstallerTests
{
    [Fact]
    public void SelectsActiveWhenCiToolMissing()
    {
        var root = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(root, "System32", "CodeIntegrity", "CiPolicies", "Active"));

        Assert.IsType<ActiveDirectoryBackend>(BackendSelector.Select(root));
    }

    [Fact]
    public void SelectsCiToolWhenPresent()
    {
        var root = TempDir.Create();
        var system32 = Path.Combine(root, "System32");
        Directory.CreateDirectory(system32);
        File.WriteAllText(Path.Combine(system32, "CiTool.exe"), string.Empty);

        Assert.IsType<CiToolBackend>(BackendSelector.Select(root));
    }

    [Fact]
    public void SelectReturnsNullWhenNeitherExists()
    {
        Assert.Null(BackendSelector.Select(TempDir.Create()));
    }

    [Fact]
    public void PlanListsEveryGroupOnce()
    {
        var manifest = TempDir.ManifestWithTwoGroups();
        var backend = new ActiveDirectoryBackend(Path.Combine(TempDir.Create(), "Active"));

        var plan = Installer.Plan(manifest, TempDir.Create(), backend);

        Assert.Equal(2, plan.Groups.Count);
    }

    [Fact]
    public void InstallWritesCipAndState()
    {
        var root = TempDir.BuiltOutput();
        var activeDir = Path.Combine(root, "Active");
        var backend = new ActiveDirectoryBackend(activeDir);
        var store = new StateStore(root);
        var manifest = TempDir.LoadManifest(root);

        var outcome = Installer.Execute(Installer.Plan(manifest, root, backend), root, backend, store, requireReboot: true);

        Assert.True(outcome.AllSucceeded);
        Assert.Single(store.Load().Policies);
        Assert.Equal("pending-reboot", store.Load().Policies[0].Status);
        Assert.Single(Directory.GetFiles(activeDir, "*.cip"));
    }

    [Fact]
    public void RemoveDeletesCipAndStateEntry()
    {
        var root = TempDir.BuiltOutput();
        var activeDir = Path.Combine(root, "Active");
        var backend = new ActiveDirectoryBackend(activeDir);
        var store = new StateStore(root);
        var manifest = TempDir.LoadManifest(root);
        Installer.Execute(Installer.Plan(manifest, root, backend), root, backend, store, requireReboot: true);

        var outcome = Installer.RemoveAll(store.Load(), backend, store);

        Assert.True(outcome.AllSucceeded);
        Assert.Empty(store.Load().Policies);
        Assert.Empty(Directory.GetFiles(activeDir, "*.cip"));
    }

    [Fact]
    public void RemoveMissingPolicyIsWarningNotFailure()
    {
        var root = TempDir.Create();
        var activeDir = Path.Combine(root, "Active");
        Directory.CreateDirectory(activeDir);
        var backend = new ActiveDirectoryBackend(activeDir);
        var store = new StateStore(root);
        store.Upsert(new StateEntry(
            "a",
            "{00000000-0000-0000-0000-000000000001}",
            "active",
            "x",
            "2026-10-28T00:00:00Z",
            "installed"));

        var outcome = Installer.RemoveAll(store.Load(), backend, store);

        Assert.True(outcome.AllSucceeded);
        Assert.Contains("未找到", outcome.Results[0].Message);
    }
}
