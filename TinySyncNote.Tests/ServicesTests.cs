using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = null!;
    private readonly AppDbContext _db = null!;
    private readonly AuthService _service = null!;

    public AuthServiceTests()
    {
        // 使用 SQLite 内存数据库（比 InMemory 更接近真实行为）
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var jwtSettings = Options.Create(new JwtSettings
        {
            Key = "TestKey_AtLeast32CharactersLongForHmac!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60
        });

        var config = new ConfigurationStub();
        _service = new AuthService(_db, jwtSettings, config);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsAuthResponse()
    {
        var request = new RegisterRequest
        {
            Username = "testuser",
            Password = "password123"
        };

        var result = await _service.RegisterAsync(request);

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("testuser", result.User.Username);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ThrowsInvalidOperation()
    {
        var request = new RegisterRequest
        {
            Username = "testuser",
            Password = "password123"
        };

        await _service.RegisterAsync(request);

        var duplicate = new RegisterRequest
        {
            Username = "testuser",
            Password = "password123"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RegisterAsync(duplicate));
        Assert.Contains("用户名已存在", ex.Message);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsAuthResponse()
    {
        var register = new RegisterRequest
        {
            Username = "testuser",
            Password = "password123"
        };
        await _service.RegisterAsync(register);

        var login = new LoginRequest
        {
            Username = "testuser",
            Password = "password123"
        };

        var result = await _service.LoginAsync(login);

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.Equal("testuser", result.User.Username);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsInvalidOperation()
    {
        var register = new RegisterRequest
        {
            Username = "testuser",
            Password = "password123"
        };
        await _service.RegisterAsync(register);

        var login = new LoginRequest
        {
            Username = "testuser",
            Password = "wrongpassword"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LoginAsync(login));
        Assert.Contains("用户名或密码错误", ex.Message);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ThrowsInvalidOperation()
    {
        var login = new LoginRequest
        {
            Username = "nonexistent",
            Password = "password123"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LoginAsync(login));
        Assert.Contains("用户名或密码错误", ex.Message);
    }
}

public class NoteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly NoteService _noteService;
    private readonly IConflictService _conflictService;
    private readonly ISnapshotService _snapshotService;
    private User _user = null!;
    private Notebook _notebook = null!;
    private Category _category = null!;

    public NoteServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        SeedData();

        var jwtSettings = Options.Create(new JwtSettings
        {
            Key = "TestKey_AtLeast32CharactersLongForHmac!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60
        });

        _conflictService = new ConflictService(_db);
        _snapshotService = new SnapshotService(_db);
        _noteService = new NoteService(_db, _conflictService, _snapshotService);
    }

    private void SeedData()
    {
        _user = new User { Username = "test" };
        _db.Users.Add(_user);
        _db.SaveChanges();

        _notebook = new Notebook { UserId = _user.Id, Name = "Test Notebook" };
        _db.Notebooks.Add(_notebook);
        _db.SaveChanges();

        _category = new Category { NotebookId = _notebook.Id, Name = "Test Category" };
        _db.Categories.Add(_category);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
    }

    [Fact]
    public async Task CreateNote_WithValidData_ReturnsNoteDetail()
    {
        var request = new CreateNoteRequest
        {
            CategoryId = _category.Id,
            Title = "Test Note",
            Content = "# Hello\nThis is a test."
        };

        var result = await _noteService.CreateAsync(_user.Id, request);

        Assert.NotNull(result);
        Assert.Equal("Test Note", result.Title);
        Assert.Equal("# Hello\nThis is a test.", result.Content);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task UpdateNote_WithCorrectVersion_Succeeds()
    {
        var create = new CreateNoteRequest
        {
            CategoryId = _category.Id,
            Title = "Test Note",
            Content = "Version 1"
        };
        var note = await _noteService.CreateAsync(_user.Id, create);

        var update = new UpdateNoteRequest
        {
            Title = "Updated Note",
            Content = "Version 2",
            Version = note.Version
        };

        var result = await _noteService.UpdateAsync(note.Id, _user.Id, update);

        Assert.Equal("Updated Note", result.Title);
        Assert.Equal("Version 2", result.Content);
        Assert.Equal(2, result.Version); // 版本号递增
    }

    [Fact]
    public async Task UpdateNote_WithWrongVersion_ThrowsConflict()
    {
        var create = new CreateNoteRequest
        {
            CategoryId = _category.Id,
            Title = "Test Note",
            Content = "Version 1"
        };
        var note = await _noteService.CreateAsync(_user.Id, create);

        var update = new UpdateNoteRequest
        {
            Title = "Attempt",
            Content = "Should conflict",
            Version = 999 // 错误的版本号
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _noteService.UpdateAsync(note.Id, _user.Id, update));
        Assert.Contains("冲突", ex.Message);
    }

    [Fact]
    public async Task DeleteNote_RemovesNoteFromDatabase()
    {
        var create = new CreateNoteRequest
        {
            CategoryId = _category.Id,
            Title = "To Delete",
            Content = "Bye"
        };
        var note = await _noteService.CreateAsync(_user.Id, create);

        await _noteService.DeleteAsync(note.Id, _user.Id);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _noteService.GetByIdAsync(note.Id, _user.Id));
    }
}

/// <summary>
/// 用于测试的 IConfiguration 最小存根
/// </summary>
public class ConfigurationStub : IConfiguration
{
    private readonly Dictionary<string, string?> _data = new()
    {
        ["Registration:Enabled"] = "true"
    };

    public string? this[string key]
    {
        get => _data.TryGetValue(key, out var v) ? v : null;
        set => _data[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
    public IChangeToken GetReloadToken() => new DummyChangeToken();
    public IConfigurationSection GetSection(string key) => new ConfigurationSection(this, key);

    private class DummyChangeToken : IChangeToken
    {
        public bool ActiveChangeCallbacks => false;
        public bool HasChanged => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new NoopDisposable();
    }

    private class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private class ConfigurationSection : IConfigurationSection
    {
        private readonly ConfigurationStub _root;
        private readonly string _key;

        public ConfigurationSection(ConfigurationStub root, string key) { _root = root; _key = key; }

        public string? this[string key]
        {
            get => _root[_key + ":" + key];
            set => _root[_key + ":" + key] = value;
        }
        public string Key => _key;
        public string Path => _key;
        public string? Value
        {
            get => _root[_key];
            set => _root[_key] = value;
        }
        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => new DummyChangeToken();
        public IConfigurationSection GetSection(string key) => new ConfigurationSection(_root, _key + ":" + key);
    }
}
