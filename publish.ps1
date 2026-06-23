# TinySyncNote 一键发布脚本
# 用法：.\publish.ps1
# 输出：项目根目录下的 Publish\ 文件夹

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$webDir = Join-Path $rootDir "TinySyncNote.Web"
$apiDir = Join-Path $rootDir "TinySyncNote.Api"
$publishDir = Join-Path $rootDir "Publish"

Write-Host "=== TinySyncNote 构建发布 ===" -ForegroundColor Cyan

# 1. 构建前端
Write-Host "[1/4] 构建前端..." -ForegroundColor Yellow
Push-Location $webDir
npm install --silent
$env:VITE_API_BASE_URL = "/note"
$env:VITE_BASE = "/note"
npm run build
if ($LASTEXITCODE -ne 0) { throw "前端构建失败" }

# 2. 复制到 API wwwroot
Write-Host "[2/4] 复制前端到 API wwwroot..." -ForegroundColor Yellow
$wwwrootDir = Join-Path $apiDir "wwwroot"
if (Test-Path $wwwrootDir) { Remove-Item -Recurse -Force $wwwrootDir }
New-Item -ItemType Directory -Path $wwwrootDir | Out-Null
Copy-Item -Recurse (Join-Path $webDir "dist\*") -Destination $wwwrootDir

# 3. 发布后端
Write-Host "[3/4] 发布后端..." -ForegroundColor Yellow
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
Push-Location $apiDir
dotnet publish -c Release -o $publishDir --nologo /p:PublishSingleFile=true --self-contained false
if ($LASTEXITCODE -ne 0) { throw "后端发布失败" }
Pop-Location

# 4. 清理前端临时文件
Write-Host "[4/4] 清理 wwwroot..." -ForegroundColor Yellow
Remove-Item -Recurse -Force $wwwrootDir
Pop-Location

Write-Host "=== 发布完成 ===" -ForegroundColor Green
Write-Host "输出目录: $publishDir" -ForegroundColor Cyan
Write-Host "运行: $publishDir\TinySyncNote.Api.exe" -ForegroundColor Cyan
