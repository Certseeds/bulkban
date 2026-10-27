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
dotnet publish (Join-Path $repoRoot 'src/bulkban/bulkban.csproj') `
    -c $Configuration `
    -r $RuntimeIdentifier `
    -o (Join-Path $repoRoot 'artifacts/publish')

exit $LASTEXITCODE
