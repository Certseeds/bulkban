# bulkban

> a cli tool to ban the application cert in Ur windows device

## 支持的系统

仅支持以下 Windows 版本(WDAC 规则/策略数量不受 32 个上限限制):

- Windows 10 22H2(build 19045+)
- Windows 11 22H2(build 22621+);Windows 11 21H2(build 22000)仍限制 32 个活动策略,不支持
- Windows Server 2022(build 20348+)

本项目**只能在 Windows 上编译**;运行时也会校验系统版本,不满足时以 exit code 2 退出。当前只产出 **win-x64** 版本。

## 开发环境

- .NET SDK 10(`global.json` 锁定了 10.0.403)
- 发布 NativeAOT 需要 Visual Studio 2022 Build Tools 的 **Desktop development with C++** 工作负载

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/bulkban -- --help

# NativeAOT 单文件发布,产物在 artifacts/publish/bulkban.exe
./scripts/publish.ps1
```

版本号取自 `package.json` 的 `version` 字段;`--version` 输出 `<version>+<git sha>`。
