using Markdig;
using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Services;

public interface IPublicShareService
{
    Task<PublicShareResponse> CreatePublicLinkAsync(Guid noteId, Guid userId, DateTime? expiresAt = null);
    Task RevokePublicLinkAsync(Guid shareId, Guid userId);
    Task<List<PublicShareResponse>> GetLinksForNoteAsync(Guid noteId, Guid userId);
    Task<PublicShareViewResponse> GetSharedNoteByTokenAsync(string token);
}

public class PublicShareService : IPublicShareService
{
    private readonly AppDbContext _db;

    public PublicShareService(AppDbContext db) => _db = db;

    public async Task<PublicShareResponse> CreatePublicLinkAsync(Guid noteId, Guid userId, DateTime? expiresAt = null)
    {
        var note = await _db.Notes
            .Include(n => n.Category).ThenInclude(c => c.Notebook)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");
        if (note.Category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        var token = GenerateToken();

        var share = new PublicShare
        {
            NoteId = noteId,
            CreatedByUserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            IsActive = true
        };
        _db.PublicShares.Add(share);
        await _db.SaveChangesAsync();

        return new PublicShareResponse
        {
            Id = share.Id,
            NoteId = share.NoteId,
            Token = share.Token,
            ShareUrl = "",
            CreatedAt = share.CreatedAt,
            ExpiresAt = share.ExpiresAt,
            IsActive = share.IsActive
        };
    }

    public async Task RevokePublicLinkAsync(Guid shareId, Guid userId)
    {
        var share = await _db.PublicShares.FindAsync(shareId)
            ?? throw new KeyNotFoundException("分享链接不存在");
        if (share.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        share.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public async Task<List<PublicShareResponse>> GetLinksForNoteAsync(Guid noteId, Guid userId)
    {
        return await _db.PublicShares
            .Where(p => p.NoteId == noteId && p.CreatedByUserId == userId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PublicShareResponse
            {
                Id = p.Id,
                NoteId = p.NoteId,
                Token = p.Token,
                ShareUrl = "",
                CreatedAt = p.CreatedAt,
                ExpiresAt = p.ExpiresAt,
                IsActive = p.IsActive
            })
            .ToListAsync();
    }

    public async Task<PublicShareViewResponse> GetSharedNoteByTokenAsync(string token)
    {
        var share = await _db.PublicShares
            .Include(p => p.Note)
            .FirstOrDefaultAsync(p => p.Token == token && p.IsActive)
            ?? throw new KeyNotFoundException("分享链接无效或已失效");

        if (share.ExpiresAt.HasValue && share.ExpiresAt.Value < DateTime.UtcNow)
        {
            share.IsActive = false;
            await _db.SaveChangesAsync();
            throw new KeyNotFoundException("分享链接已过期");
        }

        var pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var html = Markdig.Markdown.ToHtml(share.Note.Content, pipeline);

        return new PublicShareViewResponse
        {
            Title = share.Note.Title,
            HtmlContent = html,
            CreatedAt = share.Note.CreatedAt,
            UpdatedAt = share.Note.UpdatedAt
        };
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
