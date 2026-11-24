#!/usr/bin/env pwsh
# 发布 Blazor WASM 站点到 artifacts/web。
# 产物是纯静态文件,可直接用任意静态服务器托管(不需要 .NET 运行时)。
[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/bulkban.web/bulkban.web.csproj'
$wwwroot = Join-Path $repoRoot 'src/bulkban.web/bin/Release/net10.0/publish/wwwroot'
$target = Join-Path $repoRoot 'artifacts/web'

dotnet publish $project -c $Configuration

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# 静态站点只需要 wwwroot;publish 目录里的 runtimeconfig 等不属于站点内容。
Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $wwwroot '*') -Destination $target -Recurse -Force

Write-Output "站点已发布到 $target"
Write-Output "本地预览:dotnet serve -d `"$target`" 或任意静态服务器"
exit 0
