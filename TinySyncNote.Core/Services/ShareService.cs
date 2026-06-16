using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Services;

public interface IShareService
{
    Task<NoteShareResponse> ShareNoteAsync(Guid noteId, Guid ownerUserId, Guid sharedWithUserId);
    Task<List<NoteShareResponse>> GetSharesForNoteAsync(Guid noteId, Guid userId);
}

public class ShareService : IShareService
{
    private readonly AppDbContext _db;

    public ShareService(AppDbContext db) => _db = db;

    public async Task<NoteShareResponse> ShareNoteAsync(Guid noteId, Guid ownerUserId, Guid sharedWithUserId)
    {
        // 验证笔记归属
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");
        if (note.Category.Notebook.UserId != ownerUserId)
            throw new UnauthorizedAccessException("无权分享此笔记");

        // 验证目标用户存在
        var targetUser = await _db.Users.FindAsync(sharedWithUserId)
            ?? throw new KeyNotFoundException("目标用户不存在");
        if (targetUser.Id == ownerUserId)
            throw new InvalidOperationException("不能分享给自己");

        // 查重
        var existing = await _db.NoteShares
            .AnyAsync(s => s.NoteId == noteId && s.SharedWithUserId == sharedWithUserId);
        if (existing)
            throw new InvalidOperationException("已经分享给该用户");

        // 查找或创建接收方的"共享的笔记"笔记本
        var sharedNotebook = await _db.Notebooks
            .FirstOrDefaultAsync(n => n.UserId == sharedWithUserId && n.IsSystem);
        if (sharedNotebook == null)
        {
            sharedNotebook = new Notebook
            {
                UserId = sharedWithUserId,
                Name = "共享的笔记",
                Description = "自动创建的共享笔记本",
                IsSystem = true,
                SortOrder = 999
            };
            _db.Notebooks.Add(sharedNotebook);
            await _db.SaveChangesAsync();
        }

        // 查找或创建"共享"目录
        var sharedCategory = await _db.Categories
            .FirstOrDefaultAsync(c => c.NotebookId == sharedNotebook.Id && c.Name == "共享");
        if (sharedCategory == null)
        {
            sharedCategory = new Category
            {
                NotebookId = sharedNotebook.Id,
                Name = "共享",
                SortOrder = 0
            };
            _db.Categories.Add(sharedCategory);
            await _db.SaveChangesAsync();
        }

        // 复制笔记到接收方
        var sharedNote = new Note
        {
            CategoryId = sharedCategory.Id,
            Title = note.Title,
            Content = note.Content,
            Version = 1
        };
        _db.Notes.Add(sharedNote);
        await _db.SaveChangesAsync();

        // 记录分享事件
        var share = new NoteShare
        {
            NoteId = noteId,
            OwnerUserId = ownerUserId,
            SharedWithUserId = sharedWithUserId,
            SharedNoteCopyId = sharedNote.Id
        };
        _db.NoteShares.Add(share);
        await _db.SaveChangesAsync();

        var ownerUser = await _db.Users.FindAsync(ownerUserId);

        return new NoteShareResponse
        {
            Id = share.Id,
            NoteId = share.NoteId,
            OwnerUserId = share.OwnerUserId,
            OwnerUsername = ownerUser?.Username ?? "",
            SharedWithUserId = share.SharedWithUserId,
            SharedNoteCopyId = share.SharedNoteCopyId,
            SharedAt = share.SharedAt
        };
    }

    public async Task<List<NoteShareResponse>> GetSharesForNoteAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");
        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权访问");

        return await _db.NoteShares
            .Where(s => s.NoteId == noteId)
            .Join(_db.Users, s => s.SharedWithUserId, u => u.Id, (s, u) => new NoteShareResponse
            {
                Id = s.Id,
                NoteId = s.NoteId,
                OwnerUserId = s.OwnerUserId,
                OwnerUsername = u.Username,
                SharedWithUserId = s.SharedWithUserId,
                SharedNoteCopyId = s.SharedNoteCopyId,
                SharedAt = s.SharedAt
            })
            .ToListAsync();
    }
}
