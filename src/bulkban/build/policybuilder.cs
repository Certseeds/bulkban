using System.Text.Json;
using bulkban.Certs;
using bulkban.Store;
using bulkban.Wdac;

namespace bulkban.Build;

internal static class PolicyBuilder
{
    private static readonly string[] CommittedDirectories = { "xmls", "cips" };

    internal static BuildResult Build(BuildOptions options, IPowerShellHost host)
    {
        // 先完成所有读取/解析,任何失败都在写入前抛出,避免污染已有产物。
        var groups = CertScanner.Scan(options.CertsDir);
        var classified = groups.Select(CertClassifier.Classify).ToList();
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
                var entry = BuildGroup(group, staging, host, usedNames);
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
                Groups: manifestGroups);

            foreach (var skipped in skippedGroups)
            {
                warnings.Add($"[{skipped}] 该组没有受支持的证书,已跳过,不生成策略。");
            }

            File.WriteAllText(
                Path.Combine(staging, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonContext.Default.Manifest));

            Commit(staging, options.OutputDir);
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
        HashSet<string> usedNames)
    {
        // 组内没有任何可支持的证书时不生成策略:一个空的 AllowAll 策略会占用策略名额,
        // 还会误导用户以为该组已被封禁。
        if (group.Supported.Count == 0)
        {
            return null;
        }

        var xmlDir = Path.Combine(staging, "xmls");
        var cipDir = Path.Combine(staging, "cips");
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
        PolicyXml.Finalize(xmlPath, policyGuid, group.Name);

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
            Xml: "xmls/" + fileStem + ".xml",
            XmlSha256: Hashing.Sha256File(xmlPath),
            Cip: "cips/" + fileStem + ".cip",
            CipSha256: Hashing.Sha256File(cipPath));
    }

    /// <summary>
    /// 以“整体替换”的方式提交:先把旧的 xmls/、cips/、manifest.json 重命名到备份目录,
    /// 再移入新的;任何一步失败都回滚。重命名在同卷上是原子操作,进程中断后
    /// 残留的 .backup- 目录可在下次构建前恢复。
    /// </summary>
    private static void Commit(string staging, string outputDir)
    {
        RecoverIfInterrupted(outputDir);

        var backup = Path.Combine(outputDir, ".backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);

        var movedToBackup = new List<string>();
        try
        {
            foreach (var name in CommittedDirectories.Append("manifest.json"))
            {
                var existing = Path.Combine(outputDir, name);
                if (Directory.Exists(existing))
                {
                    Directory.Move(existing, Path.Combine(backup, name));
                    movedToBackup.Add(name);
                }
                else if (File.Exists(existing))
                {
                    File.Move(existing, Path.Combine(backup, name));
                    movedToBackup.Add(name);
                }
            }

            foreach (var name in CommittedDirectories)
            {
                var source = Path.Combine(staging, name);
                Directory.Move(source, Path.Combine(outputDir, name));
            }

            File.Move(
                Path.Combine(staging, "manifest.json"),
                Path.Combine(outputDir, "manifest.json"));
        }
        catch
        {
            // 回滚:删掉已移入的新文件,再把备份移回。
            foreach (var name in CommittedDirectories.Append("manifest.json"))
            {
                var current = Path.Combine(outputDir, name);
                if (Directory.Exists(current))
                {
                    Directory.Delete(current, recursive: true);
                }
                else if (File.Exists(current))
                {
                    File.Delete(current);
                }
            }

            foreach (var name in movedToBackup)
            {
                var saved = Path.Combine(backup, name);
                if (Directory.Exists(saved))
                {
                    Directory.Move(saved, Path.Combine(outputDir, name));
                }
                else if (File.Exists(saved))
                {
                    File.Move(saved, Path.Combine(outputDir, name));
                }
            }

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

    /// <summary>上次构建在提交中途中断:把残留的 .backup- 内容恢复回去。</summary>
    private static void RecoverIfInterrupted(string outputDir)
    {
        foreach (var backup in Directory.GetDirectories(outputDir, ".backup-*"))
        {
            foreach (var entry in Directory.GetFileSystemEntries(backup))
            {
                var target = Path.Combine(outputDir, Path.GetFileName(entry));
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, recursive: true);
                }
                else if (File.Exists(target))
                {
                    File.Delete(target);
                }

                if (Directory.Exists(entry))
                {
                    Directory.Move(entry, target);
                }
                else
                {
                    File.Move(entry, target);
                }
            }

            Directory.Delete(backup, recursive: true);
        }
    }
}
