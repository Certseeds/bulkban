#!/usr/bin/env pwsh
# 发布 NativeAOT 单文件 exe。
# NativeAOT 依赖 vswhere.exe 定位 MSVC;若其目录不在 PATH,
# 错误文本会被拼进 link.exe 路径,导致 MSB3073。
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
$publishDir = Join-Path $repoRoot 'artifacts/publish'

dotnet publish (Join-Path $repoRoot 'src/bulkban/bulkban.csproj') `
    -c $Configuration `
    -r $RuntimeIdentifier `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# 证书绝不能进入发布物:一旦出现 certs 目录或 .cer 文件,删除产物并失败。
$leakedCerts = @(Get-ChildItem -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ieq 'certs' -or $_.Extension -ieq '.cer' })

if ($leakedCerts.Count -gt 0) {
    Write-Error ("发布目录中出现了证书文件,已中止:{0}" -f ($leakedCerts.FullName -join ', '))
    Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

exit 0
