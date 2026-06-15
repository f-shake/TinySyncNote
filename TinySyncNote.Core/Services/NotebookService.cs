using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Services;

public interface INotebookService
{
    Task<List<NotebookResponse>> GetAllAsync(Guid userId);
    Task<NotebookResponse> GetByIdAsync(Guid id, Guid userId);
    Task<NotebookResponse> CreateAsync(Guid userId, CreateNotebookRequest request);
    Task<NotebookResponse> UpdateAsync(Guid id, Guid userId, UpdateNotebookRequest request);
    Task DeleteAsync(Guid id, Guid userId);
}

public class NotebookService : INotebookService
{
    private readonly AppDbContext _db;

    public NotebookService(AppDbContext db) => _db = db;

    public async Task<List<NotebookResponse>> GetAllAsync(Guid userId)
    {
        return await _db.Notebooks
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.CreatedAt)
            .Select(n => ToResponse(n))
            .ToListAsync();
    }

    public async Task<NotebookResponse> GetByIdAsync(Guid id, Guid userId)
    {
        var notebook = await _db.Notebooks
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId)
            ?? throw new KeyNotFoundException("笔记本不存在");
        return ToResponse(notebook);
    }

    public async Task<NotebookResponse> CreateAsync(Guid userId, CreateNotebookRequest request)
    {
        var maxOrder = await _db.Notebooks
            .Where(n => n.UserId == userId)
            .MaxAsync(n => (int?)n.SortOrder) ?? 0;

        var notebook = new Notebook
        {
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            SortOrder = maxOrder + 1
        };

        _db.Notebooks.Add(notebook);
        await _db.SaveChangesAsync();
        return ToResponse(notebook);
    }

    public async Task<NotebookResponse> UpdateAsync(Guid id, Guid userId, UpdateNotebookRequest request)
    {
        var notebook = await _db.Notebooks
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId)
            ?? throw new KeyNotFoundException("笔记本不存在");

        notebook.Name = request.Name;
        notebook.Description = request.Description;
        notebook.SortOrder = request.SortOrder;
        notebook.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToResponse(notebook);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var notebook = await _db.Notebooks
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId)
            ?? throw new KeyNotFoundException("笔记本不存在");

        _db.Notebooks.Remove(notebook);
        await _db.SaveChangesAsync();
    }

    private static NotebookResponse ToResponse(Notebook n) => new()
    {
        Id = n.Id,
        Name = n.Name,
        Description = n.Description,
        SortOrder = n.SortOrder,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}
