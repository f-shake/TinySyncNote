#!/usr/bin/env bash
# ────────────────────────────────────────────────────────────
# 切换到 PostgreSQL
# ────────────────────────────────────────────────────────────
# 用法: bash scripts/switch-to-postgresql.sh
# 前置条件: docker-compose up -d postgresql (启动 PostgreSQL 容器)
# ────────────────────────────────────────────────────────────

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== 切换到 PostgreSQL ==="

# 1. 更新 appsettings.json
echo "[1/4] 更新 appsettings.json → DatabaseProvider: Postgresql"
sed -i 's/"DatabaseProvider": "Sqlite"/"DatabaseProvider": "Postgresql"/' \
  "$ROOT/TinySyncNote.Api/appsettings.json"

# 2. 更新 Directory.Packages.props — 取消注释 Npgsql 版本
echo "[2/4] 启用 Npgsql 包版本"
sed -i 's/<!--.*<PackageVersion Include="Npgsql/<!-- ENABLED --><PackageVersion Include="Npgsql/' \
  "$ROOT/Directory.Packages.props"
sed -i 's|<PackageVersion Include="Npgsql[^>]*>.*-->|<PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />|' \
  "$ROOT/Directory.Packages.props"

# 3. 更新 Api.csproj — 取消注释 PostgreSQL ItemGroup
echo "[3/4] 启用 PostgreSQL 包引用"
# 取消注释 PostgreSQL ItemGroup（去掉开头的 <!-- 和结尾的 -->）
sed -i '/PostgreSQL（切换时取消注释/,/^  -->/{
  s/<!--//; s/-->//
}' "$ROOT/TinySyncNote.Api/TinySyncNote.Api.csproj"

# 4. 添加 ENABLE_PGSQL 编译符号
echo "[4/4] 添加 ENABLE_PGSQL 编译符号"
if ! grep -q "ENABLE_PGSQL" "$ROOT/TinySyncNote.Api/TinySyncNote.Api.csproj"; then
  sed -i '/<ImplicitUsings>/a\    <DefineConstants>$(DefineConstants);ENABLE_PGSQL</DefineConstants>' \
    "$ROOT/TinySyncNote.Api/TinySyncNote.Api.csproj"
fi

echo ""
echo "✅ 切换完成！"
echo "启动容器: docker compose up -d postgresql"
echo "运行应用: dotnet run --project TinySyncNote.Api"
