namespace bulkban.Core.Policies;

/// <summary>zip 里的一个条目;路径统一用 '/' 分隔。</summary>
internal sealed record OutputFile(string Path, byte[] Content);
