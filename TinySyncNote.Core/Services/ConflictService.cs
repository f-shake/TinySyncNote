using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;
using TinySyncNote.Core.Models.Enums;

namespace TinySyncNote.Core.Services;

public interface IConflictService
{
    Task<List<ConflictListItem>> GetUnresolvedAsync(Guid userId);
    Task<ConflictResponse> GetByIdAsync(Guid id, Guid userId);
    Task<ConflictResponse> ResolveAsync(Guid id, Guid userId, ResolveConflictRequest request);
    Task RecordConflictAsync(Guid noteId, int localVersion, int remoteVersion,
        string localContent, string remoteContent);
}

public class ConflictService : IConflictService
{
    private readonly AppDbContext _db;

    public ConflictService(AppDbContext db) => _db = db;

    public async Task<List<ConflictListItem>> GetUnresolvedAsync(Guid userId)
    {
        return await _db.NoteConflicts
            .Include(c => c.Note).ThenInclude(n => n.Category).ThenInclude(cat => cat.Notebook)
            .Where(c => c.ResolvedAt == null && c.Note.Category.Notebook.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ConflictListItem
            {
                Id = c.Id,
                NoteId = c.NoteId,
                NoteTitle = c.Note.Title,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<ConflictResponse> GetByIdAsync(Guid id, Guid userId)
    {
        var conflict = await _db.NoteConflicts
            .Include(c => c.Note)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("冲突记录不存在");

        // 验证所有权
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(cat => cat.Notebook)
            .FirstOrDefaultAsync(n => n.Id == conflict.NoteId);
        if (note == null || note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权访问");

        return MapToResponse(conflict, note.Title);
    }

    public async Task<ConflictResponse> ResolveAsync(Guid id, Guid userId, ResolveConflictRequest request)
    {
        var conflict = await _db.NoteConflicts
            .Include(c => c.Note)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("冲突记录不存在");

        // 验证所有权
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(cat => cat.Notebook)
            .FirstOrDefaultAsync(n => n.Id == conflict.NoteId);
        if (note == null || note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        if (conflict.ResolvedAt != null)
            throw new InvalidOperationException("冲突已被解决");

        var strategy = request.Strategy switch
        {
            "KeepLocal" => ConflictResolutionStrategy.KeepLocal,
            "KeepRemote" => ConflictResolutionStrategy.KeepRemote,
            "Merged" => ConflictResolutionStrategy.Merged,
            _ => throw new ArgumentException("无效的解决策略")
        };

        switch (strategy)
        {
            case ConflictResolutionStrategy.KeepLocal:
                // 本地版本即为当前笔记内容，强制升级版本号
                note.Version = Math.Max(conflict.LocalVersion, conflict.RemoteVersion) + 1;
                note.UpdatedAt = DateTime.UtcNow;
                break;

            case ConflictResolutionStrategy.KeepRemote:
                // 用远程版本覆盖
                note.Content = conflict.RemoteContent;
                note.Version = conflict.RemoteVersion + 1;
                note.UpdatedAt = DateTime.UtcNow;
                break;

            case ConflictResolutionStrategy.Merged:
                if (request.MergedContent == null)
                    throw new ArgumentException("合并模式必须提供合并内容");
                note.Content = request.MergedContent;
                note.Version = Math.Max(conflict.LocalVersion, conflict.RemoteVersion) + 1;
                note.UpdatedAt = DateTime.UtcNow;
                break;
        }

        conflict.ResolvedAt = DateTime.UtcNow;
        conflict.ResolutionStrategy = strategy;

        await _db.SaveChangesAsync();
        return MapToResponse(conflict, note.Title);
    }

    public async Task RecordConflictAsync(Guid noteId, int localVersion, int remoteVersion,
        string localContent, string remoteContent)
    {
        // 检查是否已有未解决的冲突
        var existing = await _db.NoteConflicts
            .FirstOrDefaultAsync(c => c.NoteId == noteId && c.ResolvedAt == null);
        if (existing != null) return; // 已有未解决冲突，不再重复记录

        var conflict = new NoteConflict
        {
            NoteId = noteId,
            LocalVersion = localVersion,
            RemoteVersion = remoteVersion,
            LocalContent = localContent,
            RemoteContent = remoteContent
        };

        _db.NoteConflicts.Add(conflict);
        await _db.SaveChangesAsync();
    }

    private static ConflictResponse MapToResponse(NoteConflict c, string noteTitle) => new()
    {
        Id = c.Id,
        NoteId = c.NoteId,
        NoteTitle = noteTitle,
        LocalVersion = c.LocalVersion,
        RemoteVersion = c.RemoteVersion,
        LocalContent = c.LocalContent,
        RemoteContent = c.RemoteContent,
        CreatedAt = c.CreatedAt,
        ResolvedAt = c.ResolvedAt,
        ResolutionStrategy = c.ResolutionStrategy?.ToString()
    };
}
