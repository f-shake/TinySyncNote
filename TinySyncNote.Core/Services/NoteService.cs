using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;
using TinySyncNote.Core.Models.Enums;

namespace TinySyncNote.Core.Services;

public interface INoteService
{
    Task<List<NoteListItem>> GetByCategoryAsync(Guid categoryId, Guid userId);
    Task<NoteDetailResponse> GetByIdAsync(Guid id, Guid userId);
    Task<NoteDetailResponse> CreateAsync(Guid userId, CreateNoteRequest request);
    Task<NoteDetailResponse> UpdateAsync(Guid id, Guid userId, UpdateNoteRequest request);
    Task DeleteAsync(Guid id, Guid userId);
}

public class NoteService : INoteService
{
    private readonly AppDbContext _db;
    private readonly IConflictService _conflictService;
    private readonly ISnapshotService _snapshotService;

    public NoteService(AppDbContext db, IConflictService conflictService,
        ISnapshotService snapshotService)
    {
        _db = db;
        _conflictService = conflictService;
        _snapshotService = snapshotService;
    }

    public async Task<List<NoteListItem>> GetByCategoryAsync(Guid categoryId, Guid userId)
    {
        await VerifyCategoryAccess(categoryId, userId);

        return await _db.Notes
            .Where(n => n.CategoryId == categoryId)
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteListItem
            {
                Id = n.Id,
                CategoryId = n.CategoryId,
                Title = n.Title,
                Version = n.Version,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<NoteDetailResponse> GetByIdAsync(Guid id, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("笔记不存在");

        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权访问");

        return ToDetailResponse(note);
    }

    public async Task<NoteDetailResponse> CreateAsync(Guid userId, CreateNoteRequest request)
    {
        await VerifyCategoryAccess(request.CategoryId, userId);

        var note = new Note
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            Content = request.Content,
            Version = 1
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return ToDetailResponse(note);
    }

    public async Task<NoteDetailResponse> UpdateAsync(Guid id, Guid userId, UpdateNoteRequest request)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("笔记不存在");

        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权修改");

        // 更新前自动创建快照（保留历史版本）
        await _snapshotService.CreateAsync(id, userId, SnapshotType.Automatic);

        // 乐观锁冲突检测
        if (request.Version != note.Version)
        {
            await _conflictService.RecordConflictAsync(
                id, request.Version, note.Version,
                request.Content, note.Content);
            throw new InvalidOperationException(
                $"冲突：当前版本 {note.Version}，提交版本 {request.Version}");
        }

        note.Title = request.Title;
        note.Content = request.Content;
        note.Version++;
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDetailResponse(note);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("笔记不存在");

        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权删除");

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();
    }

    private async Task VerifyCategoryAccess(Guid categoryId, Guid userId)
    {
        var hasAccess = await _db.Categories
            .Include(c => c.Notebook)
            .AnyAsync(c => c.Id == categoryId && c.Notebook.UserId == userId);

        if (!hasAccess)
            throw new KeyNotFoundException("目录不存在");
    }

    private static NoteDetailResponse ToDetailResponse(Note n) => new()
    {
        Id = n.Id,
        CategoryId = n.CategoryId,
        Title = n.Title,
        Content = n.Content,
        Version = n.Version,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}
