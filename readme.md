# bulkban

> a cli tool to ban the application cert in Ur windows device

## 支持的系统

仅支持以下 Windows 版本(WDAC 规则/策略数量不受 32 个上限限制):

- Windows 10 22H2(build 19045+)
- Windows 11 22H2(build 22621+);Windows 11 21H2(build 22000)仍限制 32 个活动策略,不支持
- Windows Server 2022(build 20348+)

本项目**只能在 Windows 上编译**;运行时也会校验系统版本,不满足时以 exit code 2 退出。当前只产出 **win-x64** 版本。

Windows Home 不在支持范围:`build` 依赖 `ConfigCI` 的 `Add-SignerRule` / `ConvertFrom-CIPolicy`。

## 用法

```text
bulkban build   [--certs-dir <dir>] [--output-dir <dir>] [--enforce]
bulkban install [--output-dir <dir>] [--dry-run] [--yes] [--force]
bulkban apply   [--certs-dir <dir>] [--output-dir <dir>] [--enforce] [--dry-run] [--yes] [--force]
bulkban verify  [--output-dir <dir>]
bulkban remove  [--output-dir <dir>] [--dry-run] [--yes]
```

- **默认生成 audit 模式**:策略只记录会被阻止的程序,不实际阻止运行。
- `--enforce`:生成 ban(强制阻止)模式;`install` 时会显示当前模式。
- `build`:按模式生成 `audit-xmls/`+`audit-cips/` 或 `enforce-xmls/`+`enforce-cips/`,以及 SHA-256 sidecar 和 `manifest.json`;不需要管理员。
- `install`:校验清单、模式目录、CIP 头和 hash 后安装;需要管理员。
- `apply`:`build` + `install`;`--dry-run` 只构建并打印计划。
- `verify`:重新校验产物完整性和模式一致性。
- `remove`:按 `state.json` 卸载。

两种模式的产物目录互相独立,先 build audit 再 build enforce 不会覆盖 audit 产物;`manifest.json` 指向最近一次构建的模式。

证书目录按以下顺序解析:`--certs-dir` → exe 同目录的 `certs` → 当前工作目录的 `certs`。

## 分组规则

- `certs/` 下每个直接子目录生成一个 policy。
- 根目录下的 `.cer` 合并为一个 policy,组名 `others`。
- 组内只读取直接包含的 `.cer`,不递归。
- 若存在名为 `others` 的子目录,构建失败并提示重命名。

## 产物布局

```text
<output-dir>/
├─ audit-xmls/<group>.xml
├─ audit-xmls/<group>.xml.sha256
├─ audit-cips/<group>.cip
├─ audit-cips/<group>.cip.sha256
├─ enforce-xmls/<group>.xml
├─ enforce-xmls/<group>.xml.sha256
├─ enforce-cips/<group>.cip
├─ enforce-cips/<group>.cip.sha256
├─ manifest.json
└─ state.json            # install/remove 维护
```

`manifest.json` 的 `mode` 决定读取哪一组目录;CIP 头里的 Audit 位必须与清单模式一致,
否则 `verify` / `install` 以 exit 6 拒绝。

每个 policy 先 Allow All,再对组内证书添加 user-mode deny signer。
PolicyID 由 UUIDv5 确定性派生,同一组重复构建不会新增策略。

## 不支持自动封禁的证书

以下证书会被跳过并在 `manifest.json` 的 `unsupported` 中记录原因:

- ECDSA/ECC(WDAC signer 规则只支持 RSA)
- RSA 密钥长度超过 4096
- CA:TRUE
- 缺少代码签名 EKU(`1.3.6.1.5.5.7.3.3`)

## 安装后端

- 系统存在 `CiTool.exe`(Windows 11 22H2+ / Server 2025+)时使用 CiTool。
- 否则复制到 `C:\Windows\System32\CodeIntegrity\CiPolicies\Active\<PolicyID>.cip`,需要重启生效。

## 退出码

| code | 含义 |
|---|---|
| 0 | 成功 |
| 1 | 通用错误 |
| 2 | 不支持的 Windows 版本 |
| 3 | 找不到证书目录或 manifest |
| 4 | 需要管理员权限 |
| 5 | 构建失败 |
| 6 | hash 校验失败 |
| 7 | 安装/卸载失败 |

## 开发环境

- .NET SDK 10(`global.json` 锁定了 10.0.403)
- 发布 NativeAOT 需要 Visual Studio 2022 Build Tools 的 MSVC 工具集与 Windows SDK

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/bulkban -- --help

# NativeAOT 单文件发布,产物在 artifacts/publish/bulkban.exe
./scripts/publish.ps1
```

`publish.ps1` 会在发布后检查产物目录,一旦出现 `certs` 目录或 `.cer` 文件就删除产物并失败。

版本号取自 `package.json` 的 `version` 字段;`--version` 输出 `<version>+<git sha>`。
