<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10"/>
  <img src="https://img.shields.io/badge/Vue-3-4FC08D?style=flat-square&logo=vue.js" alt="Vue 3"/>
  <img src="https://img.shields.io/badge/SignalR-实时同步-FF4081?style=flat-square" alt="SignalR"/>
  <img src="https://img.shields.io/badge/Vditor-Markdown-007AFF?style=flat-square" alt="Vditor"/>
  <img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="MIT License"/>
</p>

<h1 align="center">📝 TinySyncNote</h1>

<p align="center">
  <strong>跨设备实时同步的极简 Markdown 笔记应用</strong><br/>
  TinySyncNote 是一款轻量级的笔记应用，支持<strong>实时多端同步</strong>、<strong>版本历史</strong>和<strong>冲突解决</strong>。
</p>

<p align="center">
  <a href="#-功能特性">功能特性</a> •
  <a href="#-技术栈">技术栈</a> •
  <a href="#-快速开始">快速开始</a> •
  <a href="#-实时同步架构">同步架构</a> •
  <a href="#-API">API</a> •
  <a href="#-项目结构">项目结构</a>
</p>

---

## ✨ 功能特性

| 特性 | 说明 |
|------|------|
| 🔄 **实时多端同步** | 基于 SignalR WebSocket，一处修改处处可见 |
| 📝 **Markdown 编辑** | Vditor 编辑器，支持所见即所得、即时渲染、分屏预览 |
| 🤝 **冲突检测与解决** | 乐观锁版本控制，检测到冲突时提供可视化解决界面 |
| 📋 **版本历史** | 自动/手动快照，可随时回滚到任意历史版本 |
| 📂 **笔记本 + 分类** | 双层目录结构，支持无限层级子分类 |
| 🔐 **用户认证** | JWT + Refresh Token 认证体系 |
| 📤 **导入/导出** | 导出为 Markdown/ZIP，批量导入笔记 |
| 🔗 **笔记分享** | 站内分享给其他用户、生成公开链接（支持过期） |
| 🗄️ **多数据库** | 开发环境 SQLite，生产环境 PostgreSQL / MySQL |

## 🛠 技术栈

| 层级 | 技术 |
|------|------|
| **后端框架** | ASP.NET Core 10 (.NET 10) |
| **ORM** | Entity Framework Core 10 |
| **实时通信** | SignalR (WebSocket + LongPolling 回退) |
| **认证** | JWT Bearer + Refresh Token |
| **密码** | BCrypt |
| **前端框架** | Vue 3 (Composition API) |
| **状态管理** | Pinia |
| **Markdown 编辑器** | Vditor |
| **UI 组件库** | Element Plus |
| **构建工具** | Vite |
| **数据库** | SQLite / PostgreSQL / MySQL |

## 🚀 快速开始

### 前置要求

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)
- （可选）[Docker](https://www.docker.com/) — 用于 PostgreSQL/MySQL 生产环境

### 1. 启动后端

```bash
# 克隆项目
git clone <repo-url>
cd TinySyncNote

# 恢复依赖
dotnet restore

# 启动 API 服务（默认 SQLite，监听 http://localhost:5000）
cd TinySyncNote.Api
dotnet run
```

首次启动会自动创建 SQLite 数据库文件 `TinySyncNote.db`。

### 2. 启动前端

```bash
cd TinySyncNote.Web

# 安装依赖
npm install

# 开发模式（热重载，代理 API 到 :5000）
npm run dev
```

打开浏览器访问 `http://localhost:5173`。

### 3. 生产数据库配置

```bash
# 启动 PostgreSQL 或 MySQL
docker-compose up -d

# 切换到 PostgreSQL
bash scripts/switch-to-postgresql.sh

# 或者切换到 MySQL
bash scripts/switch-to-mysql.sh
```

在 `TinySyncNote.Api/appsettings.json` 中配置连接字符串。

## 🔄 实时同步架构

```
┌─────────────┐       PUT /api/notes/:id        ┌──────────────┐
│  设备 A     │ ──── (携带版本号 N) ────────→    │              │
│  (HTTP)     │                                   │  ASP.NET     │
│             │ ←── HTTP 200 (Version N+1) ───    │  Core API    │
│             │                                   │              │
│  SignalR    │ ←── NoteUpdated(N+1) ─────────   │  SignalR     │
│  (WebSocket)│      (广播给用户组)                │  Hub         │
└─────────────┘                                   └──────────────┘
                                                    │
                                                    │ NoteUpdated(N+1)
                                                    ▼
                                               ┌──────────────┐
                                               │  设备 B      │
                                               │  SignalR     │
                                               │  (WebSocket) │
                                               └──────────────┘
```

### 同步机制

1. **乐观锁并发控制** — 每篇笔记维护一个递增的 `Version` 整数
2. **保存时版本检查** — 提交的版本号必须与数据库当前版本一致，否则判定为冲突
3. **SignalR 实时广播** — 保存成功后，向同用户的所有连接推送更新事件
4. **无轮询** — 纯推送架构，省流量、响应快
5. **自动重连** — 指数退避 [0, 2s, 5s, 10s, 30s]，WebSocket 失败自动回退到 LongPolling

### 冲突处理流程

```
检测到版本不一致
        │
        ▼
记录冲突 (NoteConflict)：
  - LocalVersion / RemoteVersion
  - LocalContent / RemoteContent
        │
        ▼
返回 HTTP 409 给客户端
        │
        ▼
用户选择解决策略：
  ┌─────────────────────┐
  │ 保留我的版本        │  → 强制版本号 +1
  │ 采用服务端版本      │  → 覆盖为服务端内容
  │ 手动合并            │  → 用户编辑合并内容
  └─────────────────────┘
```

### 竞态条件防护

由于 SignalR WebSocket 消息可能比 HTTP 响应先到达，同一设备会收到自己的保存通知。
前端通过以下机制避免误报：

1. **版本号匹配**：加载笔记时将 `lastSavedVersion` 与服务端返回的版本同步
2. **在途保存追踪**：发送保存请求时记录 `pendingSaveVersion`，收到 SignalR 通知时，若 `newVersion === pendingSaveVersion + 1` 则判定为自己的保存

> 详见 [NoteEditorView.vue](TinySyncNote.Web/src/views/NoteEditorView.vue) 中的 `onNoteUpdated` 处理逻辑。

## 📡 API 总览

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/auth/register` | POST | 注册 |
| `/api/auth/login` | POST | 登录 |
| `/api/auth/refresh` | POST | 刷新令牌 |
| `/api/auth/profile` | GET | 获取当前用户信息 |
| `/api/notebooks` | GET/POST | 笔记本列表/创建 |
| `/api/notebooks/{id}` | PUT/DELETE | 更新/删除笔记本 |
| `/api/categories/tree/{notebookId}` | GET | 分类树 |
| `/api/categories` | POST | 创建分类 |
| `/api/notes/by-category/{categoryId}` | GET | 获取分类下笔记列表 |
| `/api/notes/{id}` | GET/PUT/DELETE | 笔记详情/更新/删除 |
| `/api/notes` | POST | 创建笔记 |
| `/api/conflicts` | GET | 未解决冲突列表 |
| `/api/conflicts/{id}` | GET | 冲突详情 |
| `/api/conflicts/{id}/resolve` | POST | 解决冲突 |
| `/api/notes/{noteId}/snapshots` | GET/POST | 快照列表/创建 |
| `/api/notes/{noteId}/snapshots/{id}/restore` | POST | 恢复快照 |
| `/api/export/note/{id}/markdown` | GET | 导出笔记为纯 MD（无 YAML 头部） |
| `/api/export/note/{id}/html` | GET | 导出笔记为渲染 HTML（支持 `?theme=dark`） |
| `/api/export/notebook/{id}` | GET | 导出笔记本为 ZIP |
| `/api/import/markdown` | POST | 导入 MD 文件 |
| `/api/import/zip` | POST | 导入 ZIP 压缩包 |
| `/api/users/search?q=` | GET | 搜索用户 |
| `/api/share/note/{id}` | POST | 分享笔记给其他用户 |
| `/api/share/note/{id}/public` | POST/GET | 创建/查询公开分享链接 |
| `/api/share/public/{shareId}` | DELETE | 撤销公开链接 |
| `/api/share/{token}` | GET | 查看公开分享的笔记（无需认证） |
| `/hubs/sync` | WebSocket | SignalR Sync Hub |

## 📁 项目结构

```
TinySyncNote/
├── TinySyncNote.Core/          # 领域层 + 数据层
│   ├── Data/AppDbContext.cs     # EF Core DbContext
│   ├── Models/
│   │   ├── Entities/            # User, Note, Notebook, Category ...
│   │   ├── DTOs/                # 请求/响应模型
│   │   └── Enums/               # SnapshotType, ConflictResolution
│   └── Services/                # 业务逻辑
│       ├── AuthService.cs       # 认证
│       ├── NoteService.cs       # 笔记 CRUD + 乐观锁冲突检测
│       ├── ConflictService.cs   # 冲突记录与解决
│       ├── SnapshotService.cs   # 快照管理
│       ├── NotebookService.cs   # 笔记本 CRUD
│       ├── CategoryService.cs   # 分类 CRUD + 树形结构
│       ├── ImportExportService.cs # 导入导出（MD + HTML）
│       ├── ShareService.cs      # 站内分享
│       ├── PublicShareService.cs # 公开链接
│       └── UserService.cs       # 用户搜索
├── TinySyncNote.Api/            # Web API 层
│   ├── Controllers/             # REST 控制器
│   ├── Hubs/SyncHub.cs          # SignalR Hub
│   ├── Program.cs               # 应用入口
│   └── appsettings.json         # 配置
├── TinySyncNote.Web/            # Vue 3 前端
│   └── src/
│       ├── composables/useSync.ts     # SignalR 客户端封装
│       ├── stores/                     # Pinia 状态
│       ├── views/                      # 页面组件
│       │   ├── NoteEditorView.vue      # 编辑器（含同步提示）
│       │   ├── ConflictResolverView.vue# 冲突解决界面
│       │   └── ...
│       ├── utils/http.ts               # Axios + JWT 拦截器
│       └── types/index.ts              # TypeScript 类型定义
└── TinySyncNote.Tests/          # 单元测试
    └── ServicesTests.cs
```

## 🔧 配置

### 数据库切换

项目默认使用 SQLite。通过条件编译符号切换数据库提供程序：

```xml
<!-- Directory.Packages.props 中定义编译符号 -->
<DefineConstants Condition="'$(DatabaseProvider)'=='PostgreSQL'">POSTGRESQL</DefineConstants>
<DefineConstants Condition="'$(DatabaseProvider)'=='MySQL'">MYSQL</DefineConstants>
```

```bash
# 启动 PostgreSQL
docker-compose up -d postgres
dotnet run -p TinySyncNote.Api -p:DatabaseProvider=PostgreSQL
```

### JWT 配置

在 `appsettings.json` 中配置 JWT 密钥和过期时间：

```json
{
  "JwtSettings": {
    "Key": "your-256-bit-secret-key-here",
    "Issuer": "TinySyncNote",
    "Audience": "TinySyncNote",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

## 🧪 测试

```bash
dotnet test TinySyncNote.Tests --nologo
```

测试覆盖：用户注册/登录、笔记 CRUD、乐观锁冲突检测。

## 📄 License

MIT
