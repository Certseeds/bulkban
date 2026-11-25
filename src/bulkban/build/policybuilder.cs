using System.Text;
using bulkban.Core.Policies;
using bulkban.Store;
using bulkban.Wdac;

namespace bulkban.Build;

/// <summary>
/// 把仓库/目录里的证书构造成 WDAC 产物。
/// 分组、证书解析与 XML 生成都由 <see cref="PolicyPipeline"/> 完成(与浏览器共用),
/// 这里只负责落盘、以及把 XML 转成 CIP 这一步(Windows 上仍交给 ConfigCI)。
/// </summary>
internal static class PolicyBuilder
{
    internal static BuildResult Build(BuildOptions options, IPowerShellHost host)
    {
        var mode = options.Mode;
        var certsDir = Path.GetFullPath(options.CertsDir);

        var relativePaths = Directory.EnumerateFiles(certsDir, "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cer", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetRelativePath(certsDir, p).Replace('\\', '/'))
            .ToList();

        // 分组阶段就会抛「没有 .cer」「超过单组上限」等错误,必须在写入前完成。
        var artifacts = PolicyPipeline.Build(
            relativePaths,
            relative => File.ReadAllBytes(Path.Combine(certsDir, relative.Replace('/', Path.DirectorySeparatorChar))),
            xml => ConvertXmlToCip(xml, host),
            mode,
            out var warnings);

        var warnList = warnings.ToList();

        Directory.CreateDirectory(options.OutputDir);
        var staging = Path.Combine(options.OutputDir, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var manifestGroups = new List<ManifestGroup>();
            foreach (var artifact in artifacts)
            {
                var xmlPath = Path.Combine(staging, mode.XmlDirectory(), artifact.FileStem + ".xml");
                var cipPath = Path.Combine(staging, mode.CipDirectory(), artifact.FileStem + ".cip");
                Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(cipPath)!);

                File.WriteAllText(xmlPath, artifact.Xml, new UTF8Encoding(false));
                File.WriteAllBytes(cipPath, artifact.Cip);
                Hashing.WriteSidecar(xmlPath);
                Hashing.WriteSidecar(cipPath);

                manifestGroups.Add(new ManifestGroup(
                    Name: artifact.GroupName,
                    PolicyId: "{" + artifact.PolicyId.ToString().ToUpperInvariant() + "}",
                    CertCount: artifact.CertCount,
                    Unsupported: artifact.Unsupported
                        .Select(u => new UnsupportedCert(u.FileName, u.Subject, u.UnsupportedReason ?? string.Empty))
                        .ToList(),
                    Xml: mode.XmlDirectory() + "/" + artifact.FileStem + ".xml",
                    XmlSha256: Hashing.Sha256File(xmlPath),
                    Cip: mode.CipDirectory() + "/" + artifact.FileStem + ".cip",
                    CipSha256: Hashing.Sha256File(cipPath)));
            }

            var manifest = new Manifest(
                SchemaVersion: 1,
                ToolVersion: ToolVersion.Current,
                GeneratedAt: DateTimeOffset.UtcNow.ToString("O"),
                CertsDir: certsDir,
                Mode: options.Mode.ToManifestValue(),
                Groups: manifestGroups);

            File.WriteAllText(
                Path.Combine(staging, "manifest.json"),
                System.Text.Json.JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.Manifest));

            Commit(staging, options.OutputDir, options.Mode);
            return new BuildResult(manifest, warnList);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    /// <summary>
    /// ConfigCI 只接受文件路径,所以落一个临时文件再转换。
    /// 临时文件放在系统临时目录,不污染输出目录。
    /// </summary>
    private static byte[] ConvertXmlToCip(string xml, IPowerShellHost host)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bulkban-cip", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var xmlPath = Path.Combine(tempDir, "policy.xml");
            var cipPath = Path.Combine(tempDir, "policy.cip");
            File.WriteAllText(xmlPath, xml, new UTF8Encoding(false));
            host.ConvertToCip(xmlPath, cipPath);
            return File.ReadAllBytes(cipPath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 以“整体替换”的方式提交:只替换当前模式的 xmls/cips 目录和 manifest.json;
    /// 另一种模式的目录保持不变。任何一步失败都回滚。
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
