#!/usr/bin/env pwsh
# ────────────────────────────────────────────────────────────
# 切换到 PostgreSQL
# ────────────────────────────────────────────────────────────
# 用法: .\scripts\switch-to-postgresql.ps1
# 前置条件: docker compose up -d postgresql (启动 PostgreSQL 容器)
# ────────────────────────────────────────────────────────────

$ErrorActionPreference = 'Stop'

$ROOT = Resolve-Path "$PSScriptRoot\.."

Write-Host "=== 切换到 PostgreSQL ==="

# 1. 更新 appsettings.json
Write-Host "[1/4] 更新 appsettings.json → DatabaseProvider: Postgresql"
$appsettings = Join-Path $ROOT "TinySyncNote.Api" "appsettings.json"
(Get-Content $appsettings) -replace '"DatabaseProvider": "Sqlite"', '"DatabaseProvider": "Postgresql"' | Set-Content $appsettings

# 2. 更新 Directory.Packages.props — 取消注释 Npgsql 版本
Write-Host "[2/4] 启用 Npgsql 包版本"
$props = Join-Path $ROOT "Directory.Packages.props"
$content = Get-Content $props
$content = $content -replace '<!--\s*<PackageVersion Include="Npgsql', '<PackageVersion Include="Npgsql'
$content = $content -replace '<PackageVersion Include="Npgsql[^>]*>.*-->', '<PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />'
$content | Set-Content $props

# 3. 更新 Api.csproj — 取消注释 PostgreSQL ItemGroup
Write-Host "[3/4] 启用 PostgreSQL 包引用"
$csproj = Join-Path $ROOT "TinySyncNote.Api" "TinySyncNote.Api.csproj"
$content = Get-Content $csproj

# 取消 PostgreSQL 注释块
$inBlock = $false
$newLines = @()
foreach ($line in $content) {
    if ($line -match 'PostgreSQL（切换时取消注释') {
        $inBlock = $true
        continue  # 跳过 <!-- 所在行
    }
    if ($inBlock -and $line -match '^\s*-->') {
        $inBlock = $false
        continue  # 跳过 --> 所在行
    }
    if ($inBlock) {
        $newLines += $line  # ItemGroup 内容原样保留（不含注释标记）
    } else {
        $newLines += $line
    }
}
$newLines | Set-Content $csproj

# 4. 添加 ENABLE_PGSQL 编译符号
Write-Host "[4/4] 添加 ENABLE_PGSQL 编译符号"
$content = Get-Content $csproj
if (-not ($content -match 'ENABLE_PGSQL')) {
    $content = $content -replace '<ImplicitUsings>', "<ImplicitUsings>`r`n    <DefineConstants>`$(DefineConstants);ENABLE_PGSQL</DefineConstants>"
    $content | Set-Content $csproj
}

Write-Host ""
Write-Host "✅ 切换完成！"
Write-Host "启动容器: docker compose up -d postgresql"
Write-Host "运行应用: dotnet run --project TinySyncNote.Api"
