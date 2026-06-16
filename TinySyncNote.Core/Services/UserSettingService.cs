using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Services;

public interface IUserSettingService
{
    Task<string?> GetAsync(Guid userId, string key);
    Task<Dictionary<string, string>> GetAllAsync(Guid userId);
    Task SetAsync(Guid userId, string key, string value);
    Task SetBatchAsync(Guid userId, Dictionary<string, string> settings);
}

public class UserSettingService : IUserSettingService
{
    private readonly AppDbContext _db;

    public UserSettingService(AppDbContext db) => _db = db;

    public async Task<string?> GetAsync(Guid userId, string key)
    {
        var setting = await _db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key);
        return setting?.Value;
    }

    public async Task<Dictionary<string, string>> GetAllAsync(Guid userId)
    {
        return await _db.UserSettings
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    public async Task SetAsync(Guid userId, string key, string value)
    {
        var existing = await _db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key);

        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            _db.UserSettings.Add(new UserSetting
            {
                UserId = userId,
                Key = key,
                Value = value
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetBatchAsync(Guid userId, Dictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
        {
            await SetAsync(userId, key, value);
        }
    }
}
