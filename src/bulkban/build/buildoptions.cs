using bulkban.Wdac;

namespace bulkban.Build;

internal sealed record BuildOptions(string CertsDir, string OutputDir, PolicyMode Mode = PolicyMode.Audit);

internal sealed record BuildResult(Store.Manifest Manifest, IReadOnlyList<string> Warnings);
