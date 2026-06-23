#!/usr/bin/env pwsh
# ────────────────────────────────────────────────────────────
# 切换到 MySQL
# ────────────────────────────────────────────────────────────
# 用法: .\scripts\switch-to-mysql.ps1
# 前置条件: docker compose up -d mysql (启动 MySQL 容器)
# ────────────────────────────────────────────────────────────

$ErrorActionPreference = 'Stop'

$ROOT = Resolve-Path "$PSScriptRoot\.."

Write-Host "=== 切换到 MySQL ==="

# 1. 更新 appsettings.json
Write-Host "[1/4] 更新 appsettings.json → DatabaseProvider: Mysql"
$appsettings = Join-Path $ROOT "TinySyncNote.Api" "appsettings.json"
(Get-Content $appsettings) -replace '"DatabaseProvider": "Sqlite"', '"DatabaseProvider": "Mysql"' | Set-Content $appsettings

# 2. 更新 Directory.Packages.props — 取消注释 Pomelo 版本
Write-Host "[2/4] 启用 Pomelo 包版本"
$props = Join-Path $ROOT "Directory.Packages.props"
$content = Get-Content $props
$content = $content -replace '<!--\s*<PackageVersion Include="Pomelo', '<PackageVersion Include="Pomelo'
$content = $content -replace '<PackageVersion Include="Pomelo[^>]*>.*-->', '<PackageVersion Include="Pomelo.EntityFrameworkCore.MySql" Version="10.0.0-preview.2" />'
$content | Set-Content $props

# 3. 更新 Api.csproj — 取消注释 MySQL ItemGroup
Write-Host "[3/4] 启用 MySQL 包引用"
$csproj = Join-Path $ROOT "TinySyncNote.Api" "TinySyncNote.Api.csproj"
$content = Get-Content $csproj

# 取消 MySQL 注释块
$inBlock = $false
$newLines = @()
foreach ($line in $content) {
    if ($line -match 'MySQL（切换时取消注释') {
        $inBlock = $true
        continue  # 跳过 <!-- 所在行
    }
    if ($inBlock -and $line -match '^\s*-->') {
        $inBlock = $false
        continue  # 跳过 --> 所在行
    }
    if ($inBlock) {
        $newLines += $line  # ItemGroup 内容原样保留
    } else {
        $newLines += $line
    }
}
$newLines | Set-Content $csproj

# 4. 添加 ENABLE_MYSQL 编译符号
Write-Host "[4/4] 添加 ENABLE_MYSQL 编译符号"
$content = Get-Content $csproj
if (-not ($content -match 'ENABLE_MYSQL')) {
    $content = $content -replace '<ImplicitUsings>', "<ImplicitUsings>`r`n    <DefineConstants>`$(DefineConstants);ENABLE_MYSQL</DefineConstants>"
    $content | Set-Content $csproj
}

Write-Host ""
Write-Host "✅ 切换完成！"
Write-Host "启动容器: docker compose up -d mysql"
Write-Host "运行应用: dotnet run --project TinySyncNote.Api"
