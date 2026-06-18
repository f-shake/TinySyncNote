using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models;
using TinySyncNote.Core.Services;
using TinySyncNote.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Kestrel 请求体大小无上限（由 UploadController 按配置校验）
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null;
});

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

// ────────────── 上传文件大小（由 appsettings 控制，不依赖硬编码属性） ──────────────
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
});

// ────────────── 服务注册 ──────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INotebookService, NotebookService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<IConflictService, ConflictService>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IImportExportService>(sp =>
{
    var db = sp.GetRequiredService<AppDbContext>();
    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var path = config.GetSection("AttachmentStorage")["Path"] ?? "App_Data/attachments";
    if (!Path.IsPathRooted(path))
        path = Path.Combine(env.ContentRootPath, path);
    return new ImportExportService(db, path);
});
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<IPublicShareService, PublicShareService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserSettingService, UserSettingService>();

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

// 全局解除请求体大小限制（由 UploadController 按 appsettings 校验）
app.Use((context, next) =>
{
    context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = null;
    return next();
});

// ────────────── 中间件管道 ──────────────
if (app.Environment.IsDevelopment())
{
    app.UseCors();
}

// 生产环境先改写 URL 找默认文件（index.html）
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
}
// 始终启用静态文件（上传的图片 + 生产环境的 SPA）
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SyncHub>("/hubs/sync");

// 生产环境 SPA Fallback
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

// ────────────── 确保附件存储目录存在 ──────────────
var storagePath = app.Configuration.GetSection("AttachmentStorage")["Path"] ?? "App_Data/attachments";
if (!Path.IsPathRooted(storagePath))
    storagePath = Path.Combine(app.Environment.ContentRootPath, storagePath);
Directory.CreateDirectory(storagePath);

// ────────────── 初始化数据库 ──────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

#if DEBUG
    // 开发模式：默认用户 dev / dev
    if (!db.Users.Any(u => u.Username == "dev"))
    {
        db.Users.Add(new TinySyncNote.Core.Models.Entities.User
        {
            Username = "dev",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("dev")
        });
        db.SaveChanges();
    }
#endif
}

app.Run();
