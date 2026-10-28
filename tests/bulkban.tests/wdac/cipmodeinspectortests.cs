using bulkban.Wdac;

namespace bulkban.tests.Wdac;

public class CipModeInspectorTests
{
    [Fact]
    public void DetectsAuditFromHeaderBit()
    {
        var path = WriteCip(0x89);

        Assert.Equal(PolicyMode.Audit, CipModeInspector.Detect(path));
    }

    [Fact]
    public void DetectsEnforceFromHeaderBit()
    {
        var path = WriteCip(0x88);

        Assert.Equal(PolicyMode.Enforce, CipModeInspector.Detect(path));
    }

    [Fact]
    public void ReturnsNullForShortFile()
    {
        var path = Path.Combine(TempDir.Create(), "short.cip");
        File.WriteAllBytes(path, new byte[10]);

        Assert.Null(CipModeInspector.Detect(path));
    }

    private static string WriteCip(byte optionsByte)
    {
        var path = Path.Combine(TempDir.Create(), "policy.cip");
        var bytes = new byte[64];
        bytes[CipModeInspector.OptionsOffset] = optionsByte;
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
