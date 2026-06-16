using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;

namespace TinySyncNote.Core.Services;

public interface IUserService
{
    Task<List<UserSearchResponse>> SearchUsersAsync(string query, Guid excludeUserId);
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db) => _db = db;

    public async Task<List<UserSearchResponse>> SearchUsersAsync(string query, Guid excludeUserId)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<UserSearchResponse>();

        return await _db.Users
            .Where(u => u.Id != excludeUserId
                && EF.Functions.Like(u.Username, $"%{query}%"))
            .Take(20)
            .Select(u => new UserSearchResponse
            {
                Id = u.Id,
                Username = u.Username
            })
            .ToListAsync();
    }
}
