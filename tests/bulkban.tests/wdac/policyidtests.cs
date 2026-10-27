using bulkban.Wdac;

namespace bulkban.tests.Wdac;

public class PolicyIdTests
{
    [Fact]
    public void UuidV5MatchesRfcVector()
    {
        // RFC 4122 附录 B 的标准向量使用 DNS namespace。
        var ns = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        Assert.Equal(Guid.Parse("2ed6657d-e927-568b-95e1-2665a8aea6a2"), UuidV5.Create(ns, "www.example.com"));
    }

    [Theory]
    [InlineData("2345", "6e013850-2e32-5f22-b069-209ec8df6139")]
    [InlineData("360", "d49ecc6b-5152-58d8-9128-bc288fcf986c")]
    [InlineData("tencent", "2d433bfe-1af0-5319-b7c7-33cc82468c51")]
    [InlineData("others", "845a53ca-2f58-53ef-b5fd-f0a75fe0965e")]
    public void PolicyIdIsStable(string group, string expected)
    {
        Assert.Equal(Guid.Parse(expected), PolicyId.ForGroup(group));
    }

    [Fact]
    public void PolicyIdIsCaseInsensitive()
    {
        Assert.Equal(PolicyId.ForGroup("Tencent"), PolicyId.ForGroup("tencent"));
    }
}
