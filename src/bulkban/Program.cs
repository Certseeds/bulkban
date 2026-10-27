using System.CommandLine;

namespace bulkban;

internal static class Program
{
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
            return 2;
        }

        return BuildRootCommand().Parse(args).Invoke();
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

        rootCommand.Subcommands.Add(CreatePlaceholderCommand("build", "生成 WDAC policy 中间产物。"));
        rootCommand.Subcommands.Add(CreatePlaceholderCommand("install", "安装已生成的 WDAC policy。"));
        rootCommand.Subcommands.Add(CreatePlaceholderCommand("apply", "构建并安装 WDAC policy。"));
        rootCommand.Subcommands.Add(CreatePlaceholderCommand("verify", "校验已生成产物的完整性。"));
        rootCommand.Subcommands.Add(CreatePlaceholderCommand("remove", "卸载已安装的 WDAC policy。"));

        return rootCommand;
    }

    private static Command CreatePlaceholderCommand(string name, string description)
    {
        var command = new Command(name, description);
        command.SetAction(_ => 0);
        return command;
    }
}
