using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models;
using TinySyncNote.Core.Services;
using TinySyncNote.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ────────────── 数据库（默认 SQLite） ──────────────
// ├─ 切换 PostgreSQL：取消注释 Directory.Packages.props + Api.csproj 中 Npgsql 相关行
// │  修改 appsettings.json → "DatabaseProvider": "Postgresql"
// │  连接字符串默认 localhost:5432，参见 docker-compose.yml
// ├─ 切换 MySQL：取消注释 Directory.Packages.props + Api.csproj 中 Pomelo 相关行
// │  修改 appsettings.json → "DatabaseProvider": "Mysql"
// └─ 在 Api.csproj 的 PropertyGroup 中添加对应编译符号：
//     <DefineConstants>$(DefineConstants);ENABLE_PGSQL</DefineConstants>
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
if (dbProvider == "Sqlite")
{
    var sqlitePath = builder.Configuration.GetConnectionString("Sqlite")
        ?? "Data Source=TinySyncNote.db";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(sqlitePath));
}
#if ENABLE_PGSQL
else if (dbProvider == "Postgresql")
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgresql")));
}
#endif
#if ENABLE_MYSQL
else if (dbProvider == "Mysql")
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(
            builder.Configuration.GetConnectionString("Mysql"),
            ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Mysql"))));
}
#endif

// ────────────── JWT 认证 ──────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // SignalR 支持从 QueryString 读取 Token
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ────────────── SignalR ──────────────
builder.Services.AddSignalR();

// ────────────── 服务注册 ──────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INotebookService, NotebookService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<IConflictService, ConflictService>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IImportExportService, ImportExportService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<IPublicShareService, PublicShareService>();
builder.Services.AddScoped<IUserService, UserService>();

// ────────────── CORS（开发环境允许 Vite 跨域） ──────────────
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
}

// ────────────── Controllers ──────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ────────────── 中间件管道 ──────────────
if (app.Environment.IsDevelopment())
{
    app.UseCors();
}
else
{
    // 生产环境：ASP.NET 提供前端静态文件
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SyncHub>("/hubs/sync");

// 生产环境 SPA Fallback
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

// ────────────── 自动迁移数据库 ──────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    // 兼容已有数据库：新增列和表
    try
    {
        var cols = new System.Collections.Generic.HashSet<string>();
        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Notebooks)";
            db.Database.OpenConnection();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cols.Add(reader.GetString(1));
        }
        if (!cols.Contains("IsSystem"))
            db.Database.ExecuteSqlRaw("ALTER TABLE Notebooks ADD COLUMN IsSystem INTEGER NOT NULL DEFAULT 0");
    }
    catch { }
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS NoteShares (Id TEXT NOT NULL PRIMARY KEY, NoteId TEXT NOT NULL, OwnerUserId TEXT NOT NULL, SharedWithUserId TEXT NOT NULL, SharedNoteCopyId TEXT NOT NULL, SharedAt TEXT NOT NULL)");
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS PublicShares (Id TEXT NOT NULL PRIMARY KEY, NoteId TEXT NOT NULL, CreatedByUserId TEXT NOT NULL, Token TEXT NOT NULL, CreatedAt TEXT NOT NULL, ExpiresAt TEXT NULL, IsActive INTEGER NOT NULL DEFAULT 1)");
    // 索引（SQLite 忽略重复创建）
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_NoteShares_NoteId_SharedWithUserId ON NoteShares(NoteId, SharedWithUserId)");
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_PublicShares_Token ON PublicShares(Token)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Notebooks_UserId_IsSystem ON Notebooks(UserId, IsSystem)");
}

app.Run();
