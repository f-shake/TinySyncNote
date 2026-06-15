#!/usr/bin/env bash
# ────────────────────────────────────────────────────────────
# 切换到 MySQL
# ────────────────────────────────────────────────────────────
# 用法: bash scripts/switch-to-mysql.sh
# 前置条件: docker-compose up -d mysql (启动 MySQL 容器)
# ────────────────────────────────────────────────────────────

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== 切换到 MySQL ==="

# 1. 更新 appsettings.json
echo "[1/4] 更新 appsettings.json → DatabaseProvider: Mysql"
sed -i 's/"DatabaseProvider": "Sqlite"/"DatabaseProvider": "Mysql"/' \
  "$ROOT/TinySyncNote.Api/appsettings.json"

# 2. 更新 Directory.Packages.props
echo "[2/4] 启用 Pomelo 包版本"
sed -i 's/<!--.*<PackageVersion Include="Pomelo/<!-- ENABLED --><PackageVersion Include="Pomelo/' \
  "$ROOT/Directory.Packages.props"
sed -i 's|<PackageVersion Include="Pomelo[^>]*>.*-->|<PackageVersion Include="Pomelo.EntityFrameworkCore.MySql" Version="10.0.0-preview.2" />|' \
  "$ROOT/Directory.Packages.props"

# 3. 更新 Api.csproj
echo "[3/4] 启用 MySQL 包引用"
sed -i '/MySQL（切换时取消注释/,/^  -->/{
  s/<!--//; s/-->//
}' "$ROOT/TinySyncNote.Api/TinySyncNote.Api.csproj"

# 4. 添加 ENABLE_MYSQL 编译符号
echo "[4/4] 添加 ENABLE_MYSQL 编译符号"
if ! grep -q "ENABLE_MYSQL" "$ROOT/TinySyncNote.Api/TinySyncNote.Api.csproj"; then
  sed -i '/<ImplicitUsings>/a\    <DefineConstants>$(DefineConstants);ENABLE_MYSQL</DefineConstants>' \
    "$ROOT/TinySyncNote.Api/TinySyncNote.Api.csproj"
fi

echo ""
echo "✅ 切换完成！"
echo "启动容器: docker compose up -d mysql"
echo "运行应用: dotnet run --project TinySyncNote.Api"
