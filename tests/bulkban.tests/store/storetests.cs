using System.Text.Json;
using bulkban.Store;

namespace bulkban.tests.Store;

public class StoreTests
{
    [Fact]
    public void ManifestRoundTripsThroughSourceGeneratedJson()
    {
        var manifest = new Manifest(1, "0.0.1", "2026-10-27T00:00:00Z", "C:\\certs", "audit", new[]
        {
            new ManifestGroup(
                "others",
                "{845A53CA-2F58-53EF-B5FD-F0A75FE0965E}",
                160,
                new[] { new UnsupportedCert("tuling times 256.2022.04.cer", "Tuling Times", "ECC signer rules are not supported by WDAC") },
                "audit-xmls/others.xml",
                "aa",
                "audit-cips/others.cip",
                "bb"),
        });

        var json = JsonSerializer.Serialize(manifest, JsonContext.Default.Manifest);
        var back = JsonSerializer.Deserialize(json, JsonContext.Default.Manifest)!;

        Assert.Equal(json, JsonSerializer.Serialize(back, JsonContext.Default.Manifest));
        Assert.Equal(manifest.Groups[0].PolicyId, back.Groups[0].PolicyId);
        Assert.Equal(manifest.Groups[0].Unsupported[0], back.Groups[0].Unsupported[0]);
        Assert.Contains("\"policyId\"", json);
        Assert.Contains("\"mode\": \"audit\"", json);
    }

    [Fact]
    public void StateRoundTripsThroughSourceGeneratedJson()
    {
        var state = new State(1, new[]
        {
            new StateEntry("others", "{845A53CA-2F58-53EF-B5FD-F0A75FE0965E}", "citool", "bb", "2026-10-27T00:00:00Z", "installed"),
        });

        var json = JsonSerializer.Serialize(state, JsonContext.Default.State);
        var back = JsonSerializer.Deserialize(json, JsonContext.Default.State)!;

        Assert.Equal(json, JsonSerializer.Serialize(back, JsonContext.Default.State));
        Assert.Equal(state.Policies[0], back.Policies[0]);
    }

    [Fact]
    public void SidecarMatchesSha256sumFormat()
    {
        var dir = TempDir.Create();
        var file = Path.Combine(dir, "a.bin");
        File.WriteAllBytes(file, new byte[] { 1, 2, 3 });

        Hashing.WriteSidecar(file);

        var line = File.ReadAllText(file + ".sha256");
        Assert.Matches("^[0-9a-f]{64}  ", line);
        Assert.Contains("a.bin", line);
    }

    [Fact]
    public void SidecarHashMatchesFileHash()
    {
        var dir = TempDir.Create();
        var file = Path.Combine(dir, "a.bin");
        File.WriteAllBytes(file, new byte[] { 9, 8, 7 });

        Hashing.WriteSidecar(file);

        var expected = Hashing.Sha256File(file);
        Assert.StartsWith(expected, File.ReadAllText(file + ".sha256"), StringComparison.Ordinal);
    }
}
