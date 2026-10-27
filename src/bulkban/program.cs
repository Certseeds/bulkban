using System.CommandLine;
using bulkban.Build;
using bulkban.Install;
using bulkban.Paths;
using bulkban.Wdac;

namespace bulkban;

internal static class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitGeneralError = 1;
    internal const int ExitUnsupportedWindows = 2;
    internal const int ExitCertsDirNotFound = 3;
    internal const int ExitNotAdministrator = 4;
    internal const int ExitBuildFailed = 5;
    internal const int ExitHashMismatch = 6;
    internal const int ExitInstallFailed = 7;

    private const int MinimumWindows10Build = 19045; // Windows 10 22H2
    private const int MinimumWindows11Build = 22621; // Windows 11 22H2

    private static int Main(string[] args)
    {
        // TFM 已声明 net10.0-windows...,编译期即 Windows-only;这里只校验 WDAC 所需的最低 build。
        var build = Environment.OSVersion.Version.Build;
        if (!IsSupportedWindowsBuild(build))
        {
            Console.Error.WriteLine(
                $"bulkban 需要 Windows 10 22H2 ({MinimumWindows10Build}+)、Windows 11 22H2 ({MinimumWindows11Build}+) 或 Windows Server 2022 (20348+);当前 build {build}。");
            return ExitUnsupportedWindows;
        }

        try
        {
            return BuildRootCommand().Parse(args).Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"未处理的错误:{ex.Message}");
            return ExitGeneralError;
        }
    }

    internal static bool IsSupportedWindowsBuild(int build)
    {
        // Windows 11 21H2 仍限制 32 个活动策略,必须排除。
        if (build >= 22000)
        {
            return build >= MinimumWindows11Build;
        }

        // Windows 10 22H2 为 19045,Windows Server 2022 为 20348。
        return build >= MinimumWindows10Build;
    }

    internal static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("在 Windows 设备上批量封禁应用证书。");

        rootCommand.Subcommands.Add(CreateBuildCommand());
        rootCommand.Subcommands.Add(CreateInstallCommand());
        rootCommand.Subcommands.Add(CreateApplyCommand());
        rootCommand.Subcommands.Add(CreateVerifyCommand());
        rootCommand.Subcommands.Add(CreateRemoveCommand());

        return rootCommand;
    }

    private static Option<string> CertsDirOption() => new("--certs-dir")
    {
        Description = "证书目录;默认依次查找 exe 同目录和调用目录下的 certs。",
    };

    private static Option<string> OutputDirOption() => new("--output-dir")
    {
        Description = "产物目录;默认使用证书目录的父目录。",
    };

    private static Command CreateBuildCommand()
    {
        var certsDir = CertsDirOption();
        var outputDir = OutputDirOption();
        var command = new Command("build", "生成 WDAC policy 中间产物。")
        {
            certsDir,
            outputDir,
        };

        command.SetAction(parseResult =>
            RunBuild(parseResult.GetValue(certsDir), parseResult.GetValue(outputDir)));

        return command;
    }

    private static Command CreateInstallCommand()
    {
        var outputDir = OutputDirOption();
        var dryRun = new Option<bool>("--dry-run") { Description = "只打印安装计划,不修改系统。" };
        var yes = new Option<bool>("--yes") { Description = "跳过交互确认。" };
        var force = new Option<bool>("--force") { Description = "hash 不匹配时仍然安装。" };
        var command = new Command("install", "安装已生成的 WDAC policy。")
        {
            outputDir,
            dryRun,
            yes,
            force,
        };

        command.SetAction(parseResult => RunInstall(
            parseResult.GetValue(outputDir),
            parseResult.GetValue(dryRun),
            parseResult.GetValue(force),
            parseResult.GetValue(yes)));

        return command;
    }

    private static Command CreateApplyCommand()
    {
        var certsDir = CertsDirOption();
        var outputDir = OutputDirOption();
        var dryRun = new Option<bool>("--dry-run") { Description = "只构建并打印安装计划。" };
        var yes = new Option<bool>("--yes") { Description = "跳过交互确认。" };
        var force = new Option<bool>("--force") { Description = "hash 不匹配时仍然安装。" };
        var command = new Command("apply", "构建并安装 WDAC policy。")
        {
            certsDir,
            outputDir,
            dryRun,
            yes,
            force,
        };

        command.SetAction(parseResult =>
        {
            var certs = parseResult.GetValue(certsDir);
            var output = parseResult.GetValue(outputDir);
            var isDryRun = parseResult.GetValue(dryRun);

            var buildExit = RunBuild(certs, output);
            if (buildExit != ExitSuccess)
            {
                return buildExit;
            }

            return RunInstall(output, isDryRun, parseResult.GetValue(force), parseResult.GetValue(yes));
        });

        return command;
    }

    private static Command CreateVerifyCommand()
    {
        var outputDir = OutputDirOption();
        var command = new Command("verify", "校验已生成产物的完整性。") { outputDir };

        command.SetAction(parseResult => RunVerify(parseResult.GetValue(outputDir)));

        return command;
    }

    private static Command CreateRemoveCommand()
    {
        var outputDir = OutputDirOption();
        var dryRun = new Option<bool>("--dry-run") { Description = "只打印将卸载的策略。" };
        var yes = new Option<bool>("--yes") { Description = "跳过交互确认。" };
        var command = new Command("remove", "卸载已安装的 WDAC policy。") { outputDir, dryRun, yes };

        command.SetAction(parseResult => RunRemove(
            parseResult.GetValue(outputDir),
            parseResult.GetValue(dryRun),
            parseResult.GetValue(yes)));

        return command;
    }

    private static int RunBuild(string? certsOption, string? outputOption)
    {
        string certsDir;
        string outputDir;
        try
        {
            certsDir = PathResolver.ResolveCertsDir(
                certsOption,
                AppContext.BaseDirectory,
                Environment.CurrentDirectory);
            outputDir = PathResolver.ResolveOutputDir(
                outputOption,
                certsDir,
                Environment.CurrentDirectory);
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCertsDirNotFound;
        }

        try
        {
            var result = PolicyBuilder.Build(new BuildOptions(certsDir, outputDir), new PowerShellHost());
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"警告:{warning}");
            }

            Console.WriteLine($"已生成 {result.Manifest.Groups.Count} 个 policy 到 {outputDir}");
            foreach (var group in result.Manifest.Groups)
            {
                Console.WriteLine($"  {group.Name}:{group.CertCount} 张证书,{group.PolicyId}");
            }

            return ExitSuccess;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"构建失败:{ex.Message}");
            return ExitBuildFailed;
        }
    }

    private static int RunVerify(string? outputOption)
    {
        var outputDir = outputOption ?? Environment.CurrentDirectory;
        var errors = Verifier.Verify(outputDir);
        if (errors.Count == 0)
        {
            Console.WriteLine("校验通过。");
            return ExitSuccess;
        }

        foreach (var error in errors)
        {
            Console.Error.WriteLine(error);
        }

        return ExitHashMismatch;
    }

    private static int RunInstall(string? outputOption, bool dryRun, bool force, bool yes)
    {
        var outputDir = outputOption ?? Environment.CurrentDirectory;
        var manifestPath = Path.Combine(outputDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"找不到 manifest.json:{manifestPath}");
            return ExitCertsDirNotFound;
        }

        var manifest = ReadManifest(manifestPath);
        if (manifest is null)
        {
            Console.Error.WriteLine($"manifest.json 无法解析:{manifestPath}");
            return ExitHashMismatch;
        }

        var errors = Verifier.Verify(outputDir);
        if (errors.Count > 0 && !force)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            return ExitHashMismatch;
        }

        var backend = BackendSelector.Select(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (backend is null)
        {
            Console.Error.WriteLine("未找到 CiTool.exe,也没有可用的 CiPolicies\\Active 目录。");
            return ExitInstallFailed;
        }

        PrintInstallPlan(manifest, backend.Name);

        if (dryRun)
        {
            return ExitSuccess;
        }

        if (!IsAdministrator())
        {
            Console.Error.WriteLine("install 需要管理员权限。");
            return ExitNotAdministrator;
        }

        if (!yes && !Confirm("确认安装以上策略?此操作会修改本机 WDAC 配置。"))
        {
            Console.WriteLine("已取消。");
            return ExitGeneralError;
        }

        var requiresReboot = backend.Name == "active";
        var store = new StateStore(outputDir);
        var outcome = Installer.Execute(
            Installer.Plan(manifest, Environment.GetFolderPath(Environment.SpecialFolder.Windows), backend),
            outputDir,
            backend,
            store,
            requiresReboot);

        foreach (var (group, success, message) in outcome.Results)
        {
            Console.WriteLine($"{(success ? "OK" : "失败")} {group}:{message}");
        }

        if (!outcome.AllSucceeded)
        {
            return ExitInstallFailed;
        }

        if (requiresReboot)
        {
            Console.WriteLine("策略已写入 Active 目录,需要重启后生效。");
        }

        return ExitSuccess;
    }

    private static int RunRemove(string? outputOption, bool dryRun, bool yes)
    {
        var outputDir = outputOption ?? Environment.CurrentDirectory;
        var store = new StateStore(outputDir);
        var state = store.Load();

        if (state.Policies.Count == 0)
        {
            // state.json 丢失时回退到 manifest,否则用户会以为已经卸载成功。
            var manifestPath = Path.Combine(outputDir, "manifest.json");
            var manifest = File.Exists(manifestPath) ? ReadManifest(manifestPath) : null;
            if (manifest is null)
            {
                Console.WriteLine("没有记录到已安装的策略,也没有可用的 manifest.json。");
                return ExitSuccess;
            }

            Console.Error.WriteLine("警告:找不到 state.json,回退到 manifest.json;改名或删除过的旧策略可能无法清理。");
            state = new Store.State(1, manifest.Groups
                .Select(g => new Store.StateEntry(g.Name, g.PolicyId, string.Empty, g.CipSha256, string.Empty, "unknown"))
                .ToList());
        }

        if (dryRun)
        {
            foreach (var entry in state.Policies)
            {
                Console.WriteLine($"  将移除 {entry.Name} -> {entry.PolicyId}");
            }

            return ExitSuccess;
        }

        if (!IsAdministrator())
        {
            Console.Error.WriteLine("remove 需要管理员权限。");
            return ExitNotAdministrator;
        }

        var backend = BackendSelector.Select(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (backend is null)
        {
            Console.Error.WriteLine("未找到 CiTool.exe,也没有可用的 CiPolicies\\Active 目录。");
            return ExitInstallFailed;
        }

        if (!yes && !Confirm($"确认移除 {state.Policies.Count} 个策略?"))
        {
            Console.WriteLine("已取消。");
            return ExitGeneralError;
        }

        var outcome = Installer.RemoveAll(state, backend, store);
        foreach (var (group, success, message) in outcome.Results)
        {
            Console.WriteLine($"{(success ? "OK" : "失败")} {group}:{message}");
        }

        return outcome.AllSucceeded ? ExitSuccess : ExitInstallFailed;
    }

    private static Store.Manifest? ReadManifest(string path)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(
                File.ReadAllText(path),
                Store.JsonContext.Default.Manifest);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void PrintInstallPlan(Store.Manifest manifest, string backendName)
    {
        Console.WriteLine($"后端:{backendName}");
        foreach (var group in manifest.Groups)
        {
            var unsupported = group.Unsupported.Count == 0
                ? string.Empty
                : $",跳过 {group.Unsupported.Count} 张不支持的证书";
            Console.WriteLine($"  {group.Name}:{group.CertCount} 张证书{unsupported},{group.PolicyId}");
        }
    }

    private static bool Confirm(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine($"{prompt} 当前无法交互确认,请加 --yes。");
            return false;
        }

        Console.Write($"{prompt} [y/N] ");
        return string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
