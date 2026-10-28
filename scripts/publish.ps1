#!/usr/bin/env pwsh
# 发布 NativeAOT 单文件 exe。
# NativeAOT 依赖 vswhere.exe 定位 MSVC;若其目录不在 PATH,
# 错误文本会被拼进 link.exe 路径,导致 MSB3073。
# 发布物不包含 PDB,避免把本机绝对路径嵌进 exe。
[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path (Join-Path $vsInstaller 'vswhere.exe')) {
    $env:PATH = "$vsInstaller;$env:PATH"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot 'src/bulkban'
$publishDir = Join-Path $repoRoot 'artifacts/publish'

# 清掉旧产物,避免上一次的 PDB / exe 混入。
Remove-Item -LiteralPath (Join-Path $projectDir 'bin') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $projectDir 'obj') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish (Join-Path $projectDir 'bulkban.csproj') `
    -c $Configuration `
    -r $RuntimeIdentifier `
    -o $publishDir `
    -p:NativeDebugSymbols=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# PDB 会包含源码绝对路径;exe 的调试目录也会嵌入 PDB 路径。
$pdbs = @(Get-ChildItem -LiteralPath $publishDir -Recurse -Force -Filter *.pdb -ErrorAction SilentlyContinue)
if ($pdbs.Count -gt 0) {
    [Console]::Error.WriteLine(("发布目录中出现了 PDB,已中止:{0}" -f ($pdbs.FullName -join ', ')))
    Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

# 证书绝不能进入发布物:一旦出现 certs 目录或 .cer 文件,删除产物并失败。
$leakedCerts = @(Get-ChildItem -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ieq 'certs' -or $_.Extension -ieq '.cer' })
if ($leakedCerts.Count -gt 0) {
    [Console]::Error.WriteLine(("发布目录中出现了证书文件,已中止:{0}" -f ($leakedCerts.FullName -join ', ')))
    Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

# 双保险:exe 里不应出现当前用户的 profile 绝对路径。
$exePath = Join-Path $publishDir 'bulkban.exe'
if ((Test-Path -LiteralPath $exePath) -and -not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    $profileHit = & findstr.exe /M "/C:$env:USERPROFILE" $exePath 2>$null
    if ($profileHit) {
        [Console]::Error.WriteLine(("发布物中包含本机路径 '{0}',已中止。" -f $env:USERPROFILE))
        Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
        exit 1
    }
}

exit 0
