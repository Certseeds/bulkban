# bulkban

> a cli tool to ban the application cert in Ur windows device

## 支持的系统

仅支持以下 Windows 版本(安装 2024-04-09 或之后的安全更新后,WDAC 规则/策略数量不受 32 个上限限制):

- Windows 10 22H2(build 19045+)
- Windows 11 22H2(build 22621+);Windows 11 21H2(build 22000)仍限制 32 个活动策略,不支持
- Windows Server 2022(build 20348+)

> 32 个活动策略上限需要安装 2024-04-09 或之后的安全更新才会解除;未安装时超过上限(系统自带的 inbox 策略也计入)可能导致 `ci.dll` 蓝屏,错误码 `0x0000003b`。

本项目**只能在 Windows 上编译**;运行时也会校验系统版本和补丁级别(注册表 UBR),不满足时以 exit code 2 退出。当前只产出 **win-x64** 版本。

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
- 根目录下的 `.cer` 与 `certs/others/` 里的 `.cer` 合并为一个 policy,组名 `others`。
- 组内只读取直接包含的 `.cer`,不递归。
- `certs/others/` 只允许放 `.cer`(按扩展名判断);出现其他文件或子目录时构建失败。
- 单个证书组最多 **150** 个 `.cer`(普通组即一个直接子目录;`others` 按根目录与 `certs/others/` 合计)。超过时 `build` 直接失败并提示拆分。
  - 原因:单个 policy 的 signer 规则过多会让 ConfigCI 的 `ConvertFrom-CIPolicy` 抛 `System.OutOfMemoryException`。
  - 拆分后的每个子目录会生成独立 policy; 活动策略数量限制取决于系统补丁状态,详见上文支持系统说明。
  - 示例:保留 `certs/others/`(不超过 150 个),把其余证书放进 `certs/others-2/`、`certs/others-3/`;
    - 根目录下的 `.cer` 也会并入 `others` 组,需要一并移入新子目录。

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
| 2 | 不支持的 Windows 版本(含未安装 2024-04-09 或之后安全更新的系统) |
| 3 | 找不到证书目录或 manifest |
| 4 | 需要管理员权限 |
| 5 | 构建失败 |
| 6 | hash 校验失败 |
| 7 | 安装/卸载失败 |

## 开发环境

- .NET SDK 10(`global.json` 锁定了 10.0.404)
- 发布 NativeAOT 需要 Visual Studio 2022 Build Tools 的 MSVC 工具集与 Windows SDK

### 项目结构

| 项目 | TFM | 用途 |
|---|---|---|
| `src/bulkban.core` | `net10.0` | 跨平台核心:证书解析、TBS 哈希、policy XML 生成 |
| `src/bulkban` | `net10.0-windows10.0.19041.0` | Windows CLI:构建、安装、校验 |
| `src/bulkban.web` | `net10.0`(Blazor WASM) | 浏览器端:拖入 zip → 输出 zip,全程本地处理 |

浏览器端与 Windows 端共用 `bulkban.core` 的分组、证书解析与 XML 生成;

唯一的差异是 XML → CIP:Windows 交给 `ConfigCI`,浏览器交给 `Microsoft.Security.CodeIntegrity`。

### 网页版

```powershell
./scripts/publish-web.ps1     # 静态站点输出到 artifacts/web
```

拖入一个内含 `certs/` 目录的 zip,产出 zip 同时包含:

```text
audit-cips/  audit-xmls/  enforce-cips/  enforce-xmls/
```

每个 `.cip` / `.xml` 旁边带 `.sha256`。文件不上传,全部在浏览器里处理。

`bulkban.core` 刻意**不使用** `System.Security.Cryptography.X509Certificates`:

该 API 在 browser 平台不可用,且拿不到 TBSCertificate 的原始编码。
证书解析走自带的 DER 解析器(`asn1/derreader.cs`),TBS 哈希算法跟随证书自身的
signatureAlgorithm(与 ConfigCI 一致):SHA-256 证书用 SHA-256,SHA-1 证书用 SHA-1。

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/bulkban -- --help

# NativeAOT 单文件发布,产物在 artifacts/publish/bulkban.exe
./scripts/publish.ps1
```

`publish.ps1` 会在发布后检查产物目录,一旦出现 `certs` 目录或 `.cer` 文件就删除产物并失败。

`certs/` 不进版本库。依赖它的测试(真实证书对拍、SHA-1/SHA-384 用例)在本机有该目录时才会执行, CI 上没有样本会自动跳过;因此 CI 绿不代表这些用例跑过。

版本号取自 `package.json` 的 `version` 字段;`--version` 输出 `<version>+<git sha>`。
