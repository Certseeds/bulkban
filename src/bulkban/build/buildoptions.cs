namespace bulkban.Build;

internal sealed record BuildOptions(string CertsDir, string OutputDir);

internal sealed record BuildResult(Store.Manifest Manifest, IReadOnlyList<string> Warnings);
