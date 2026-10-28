using System.Text.Json;
using bulkban.Certs;
using bulkban.Store;
using bulkban.Wdac;

namespace bulkban.Build;

internal static class PolicyBuilder
{
    internal static BuildResult Build(BuildOptions options, IPowerShellHost host)
    {
        // 先完成所有读取/解析,任何失败都在写入前抛出,避免污染已有产物。
        var groups = CertScanner.Scan(options.CertsDir);
        var classified = groups.Select(CertClassifier.Classify).ToList();
        var modeName = options.Mode.ToManifestValue();
        var warnings = classified
            .SelectMany(g => g.Unsupported.Select(u => $"[{g.Name}] 跳过 {u.FileName}:{u.UnsupportedReason}"))
            .ToList();

        Directory.CreateDirectory(options.OutputDir);
        var staging = Path.Combine(options.OutputDir, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var manifestGroups = new List<ManifestGroup>();
            var skippedGroups = new List<string>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in classified)
            {
                var entry = BuildGroup(group, staging, host, usedNames, options.Mode);
                if (entry is null)
                {
                    skippedGroups.Add(group.Name);
                    continue;
                }

                manifestGroups.Add(entry);
            }

            var manifest = new Manifest(
                SchemaVersion: 1,
                ToolVersion: ToolVersion.Current,
                GeneratedAt: DateTimeOffset.UtcNow.ToString("O"),
                CertsDir: Path.GetFullPath(options.CertsDir),
                Mode: modeName,
                Groups: manifestGroups);

            foreach (var skipped in skippedGroups)
            {
                warnings.Add($"[{skipped}] 该组没有受支持的证书,已跳过,不生成策略。");
            }

            File.WriteAllText(
                Path.Combine(staging, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonContext.Default.Manifest));

            Commit(staging, options.OutputDir, options.Mode);
            return new BuildResult(manifest, warnings);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private static ManifestGroup? BuildGroup(
        ClassifiedGroup group,
        string staging,
        IPowerShellHost host,
        HashSet<string> usedNames,
        PolicyMode mode)
    {
        // 组内没有任何可支持的证书时不生成策略:一个空的 AllowAll 策略会占用策略名额,
        // 还会误导用户以为该组已被封禁。
        if (group.Supported.Count == 0)
        {
            return null;
        }

        var xmlDir = Path.Combine(staging, mode.XmlDirectory());
        var cipDir = Path.Combine(staging, mode.CipDirectory());
        Directory.CreateDirectory(xmlDir);
        Directory.CreateDirectory(cipDir);

        var fileStem = SafeFileName.Unique(group.Name, usedNames);
        var xmlPath = Path.Combine(xmlDir, fileStem + ".xml");
        var cipPath = Path.Combine(cipDir, fileStem + ".cip");

        AllowAllTemplate.WriteTo(xmlPath);

        foreach (var cert in group.Supported)
        {
            host.AddDenySigner(xmlPath, cert.FullPath);
        }

        // Finalize 必须早于 ConvertToCip:PolicyID、规则选项和 ID 规范化都会写入 XML,
        // 而 ConvertFrom-CIPolicy 会把当时的 XML 固化进 CIP。
        var policyGuid = PolicyId.ForGroup(group.Name);
        PolicyXml.Finalize(xmlPath, policyGuid, group.Name, mode);

        host.ConvertToCip(xmlPath, cipPath);

        Hashing.WriteSidecar(xmlPath);
        Hashing.WriteSidecar(cipPath);

        return new ManifestGroup(
            Name: group.Name,
            PolicyId: "{" + policyGuid.ToString().ToUpperInvariant() + "}",
            CertCount: group.Supported.Count,
            Unsupported: group.Unsupported
                .Select(u => new UnsupportedCert(u.FileName, u.Subject, u.UnsupportedReason ?? string.Empty))
                .ToList(),
            Xml: mode.XmlDirectory() + "/" + fileStem + ".xml",
            XmlSha256: Hashing.Sha256File(xmlPath),
            Cip: mode.CipDirectory() + "/" + fileStem + ".cip",
            CipSha256: Hashing.Sha256File(cipPath));
    }

    /// <summary>
    /// 以“整体替换”的方式提交:只替换 xmls/、当前模式的 cips/&lt;mode&gt;/ 和 manifest.json;
    /// 另一种模式的 CIP 目录保持不变。任何一步失败都回滚。
    /// </summary>
    private static void Commit(string staging, string outputDir, PolicyMode mode)
    {
        RecoverIfInterrupted(outputDir);

        var backup = Path.Combine(outputDir, ".backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var paths = CommittedPaths(mode);

        try
        {
            foreach (var relative in paths)
            {
                var existing = Path.Combine(outputDir, relative);
                var saved = Path.Combine(backup, relative);
                if (Directory.Exists(existing))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    Directory.Move(existing, saved);
                }
                else if (File.Exists(existing))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Move(existing, saved);
                }
            }

            Directory.Move(
                Path.Combine(staging, mode.XmlDirectory()),
                Path.Combine(outputDir, mode.XmlDirectory()));
            Directory.Move(
                Path.Combine(staging, mode.CipDirectory()),
                Path.Combine(outputDir, mode.CipDirectory()));
            File.Move(
                Path.Combine(staging, "manifest.json"),
                Path.Combine(outputDir, "manifest.json"));
        }
        catch
        {
            foreach (var relative in paths)
            {
                DeletePath(Path.Combine(outputDir, relative));
            }

            RestoreBackup(backup, outputDir);
            throw;
        }
        finally
        {
            if (Directory.Exists(backup))
            {
                Directory.Delete(backup, recursive: true);
            }
        }
    }

    private static string[] CommittedPaths(PolicyMode mode) =>
        new[] { mode.XmlDirectory(), mode.CipDirectory(), "manifest.json" };

    private static void DeletePath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>上次构建在提交中途中断:把残留 .backup- 的内容按顶层路径恢复。</summary>
    private static void RecoverIfInterrupted(string outputDir)
    {
        foreach (var backup in Directory.GetDirectories(outputDir, ".backup-*"))
        {
            RestoreBackup(backup, outputDir);
            Directory.Delete(backup, recursive: true);
        }
    }

    /// <summary>备份目录里只有当前模式的 xmls/cips 目录和 manifest.json,按顶层路径恢复。</summary>
    private static void RestoreBackup(string backup, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var entry in Directory.GetFileSystemEntries(backup))
        {
            var targetPath = Path.Combine(outputDir, Path.GetFileName(entry));
            DeletePath(targetPath);
            MoveEntry(entry, targetPath);
        }
    }

    private static void MoveEntry(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (Directory.Exists(source))
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target, overwrite: true);
        }
    }
}
