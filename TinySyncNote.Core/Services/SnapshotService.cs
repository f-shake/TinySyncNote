using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;
using TinySyncNote.Core.Models.Enums;

namespace TinySyncNote.Core.Services;

public interface ISnapshotService
{
    Task<List<SnapshotListItem>> GetByNoteAsync(Guid noteId, Guid userId);
    Task<SnapshotResponse> GetByIdAsync(Guid noteId, Guid snapshotId, Guid userId);
    Task<SnapshotResponse> CreateAsync(Guid noteId, Guid userId, SnapshotType snapshotType = SnapshotType.Manual);
    Task<SnapshotResponse> RestoreAsync(Guid noteId, Guid snapshotId, Guid userId);
    Task DeleteAsync(Guid noteId, Guid snapshotId, Guid userId);
}

public class SnapshotService : ISnapshotService
{
    private readonly AppDbContext _db;

    public SnapshotService(AppDbContext db) => _db = db;

    public async Task<List<SnapshotListItem>> GetByNoteAsync(Guid noteId, Guid userId)
    {
        await VerifyNoteAccess(noteId, userId);

        return await _db.NoteSnapshots
            .Where(s => s.NoteId == noteId)
            .OrderByDescending(s => s.SnapshotAt)
            .Select(s => new SnapshotListItem
            {
                Id = s.Id,
                Title = s.Title,
                Version = s.Version,
                SnapshotType = s.SnapshotType.ToString(),
                SnapshotAt = s.SnapshotAt,
                ContentLength = s.Content.Length
            })
            .ToListAsync();
    }

    public async Task<SnapshotResponse> GetByIdAsync(Guid noteId, Guid snapshotId, Guid userId)
    {
        await VerifyNoteAccess(noteId, userId);

        var snapshot = await _db.NoteSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.NoteId == noteId)
            ?? throw new KeyNotFoundException("快照不存在");

        return ToResponse(snapshot);
    }

    public async Task<SnapshotResponse> CreateAsync(Guid noteId, Guid userId, SnapshotType snapshotType = SnapshotType.Manual)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        var snapshot = new NoteSnapshot
        {
            NoteId = noteId,
            Title = note.Title,
            Content = note.Content,
            Version = note.Version,
            FormatVersion = note.FormatVersion,
            SnapshotType = snapshotType
        };

        _db.NoteSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();

        return ToResponse(snapshot);
    }

    public async Task<SnapshotResponse> RestoreAsync(Guid noteId, Guid snapshotId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        var snapshot = await _db.NoteSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.NoteId == noteId)
            ?? throw new KeyNotFoundException("快照不存在");

        // 创建当前版本的快照（备份）
        var backup = new NoteSnapshot
        {
            NoteId = noteId,
            Title = note.Title,
            Content = note.Content,
            Version = note.Version,
            FormatVersion = note.FormatVersion,
            SnapshotType = SnapshotType.Automatic
        };
        _db.NoteSnapshots.Add(backup);

        // 恢复快照内容到笔记
        note.Title = snapshot.Title;
        note.Content = snapshot.Content;
        note.Version++;
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ToResponse(snapshot);
    }

    public async Task DeleteAsync(Guid noteId, Guid snapshotId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        var snapshot = await _db.NoteSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.NoteId == noteId)
            ?? throw new KeyNotFoundException("快照不存在");

        _db.NoteSnapshots.Remove(snapshot);
        await _db.SaveChangesAsync();
    }

    private async Task VerifyNoteAccess(Guid noteId, Guid userId)
    {
        var hasAccess = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .AnyAsync(n => n.Id == noteId && n.Category.Notebook.UserId == userId);
        if (!hasAccess)
            throw new KeyNotFoundException("笔记不存在");
    }

    private static SnapshotResponse ToResponse(NoteSnapshot s) => new()
    {
        Id = s.Id,
        NoteId = s.NoteId,
        Title = s.Title,
        Content = s.Content,
        Version = s.Version,
        SnapshotType = s.SnapshotType.ToString(),
        SnapshotAt = s.SnapshotAt
    };
}
