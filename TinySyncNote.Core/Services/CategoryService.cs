using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Services;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetTreeAsync(Guid notebookId, Guid userId);
    Task<CategoryResponse> CreateAsync(Guid userId, CreateCategoryRequest request);
    Task<CategoryResponse> UpdateAsync(Guid id, Guid userId, UpdateCategoryRequest request);
    Task DeleteAsync(Guid id, Guid userId);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db) => _db = db;

    public async Task<List<CategoryResponse>> GetTreeAsync(Guid notebookId, Guid userId)
    {
        // 验证笔记本归属
        var notebookExists = await _db.Notebooks
            .AnyAsync(n => n.Id == notebookId && n.UserId == userId);
        if (!notebookExists)
            throw new KeyNotFoundException("笔记本不存在");

        var categories = await _db.Categories
            .Where(c => c.NotebookId == notebookId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();

        var noteCounts = await _db.Notes
            .Where(n => n.Category.NotebookId == notebookId)
            .GroupBy(n => n.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        return BuildTree(categories, null, noteCounts);
    }

    public async Task<CategoryResponse> CreateAsync(Guid userId, CreateCategoryRequest request)
    {
        // 验证笔记本归属
        var notebookExists = await _db.Notebooks
            .AnyAsync(n => n.Id == request.NotebookId && n.UserId == userId);
        if (!notebookExists)
            throw new KeyNotFoundException("笔记本不存在");

        // 如果指定了父目录，验证父目录归属
        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _db.Categories
                .AnyAsync(c => c.Id == request.ParentCategoryId
                            && c.NotebookId == request.NotebookId);
            if (!parentExists)
                throw new KeyNotFoundException("父目录不存在");
        }

        // 检查同层同名
        var duplicate = await _db.Categories.AnyAsync(c =>
            c.NotebookId == request.NotebookId
            && c.ParentCategoryId == request.ParentCategoryId
            && c.Name == request.Name);
        if (duplicate)
            throw new InvalidOperationException("同一目录下已存在同名目录");

        var maxOrder = await _db.Categories
            .Where(c => c.NotebookId == request.NotebookId
                     && c.ParentCategoryId == request.ParentCategoryId)
            .MaxAsync(c => (int?)c.SortOrder) ?? 0;

        var category = new Category
        {
            NotebookId = request.NotebookId,
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name,
            SortOrder = maxOrder + 1
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return ToResponse(category, 0);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid id, Guid userId, UpdateCategoryRequest request)
    {
        var category = await _db.Categories
            .Include(c => c.Notebook)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("目录不存在");

        if (category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        // 检查同层同名（排除自身）
        var duplicate = await _db.Categories.AnyAsync(c =>
            c.Id != id
            && c.NotebookId == category.NotebookId
            && c.ParentCategoryId == request.ParentCategoryId
            && c.Name == request.Name);
        if (duplicate)
            throw new InvalidOperationException("同一目录下已存在同名目录");

        category.Name = request.Name;
        category.SortOrder = request.SortOrder;
        category.ParentCategoryId = request.ParentCategoryId;
        category.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToResponse(category, 0);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var category = await _db.Categories
            .Include(c => c.Notebook)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("目录不存在");

        if (category.Notebook.UserId != userId)
            throw new UnauthorizedAccessException("无权操作");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
    }

    private static List<CategoryResponse> BuildTree(
        List<Category> flat,
        Guid? parentId,
        Dictionary<Guid, int> noteCounts)
    {
        return flat
            .Where(c => c.ParentCategoryId == parentId)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                NotebookId = c.NotebookId,
                ParentCategoryId = c.ParentCategoryId,
                Name = c.Name,
                SortOrder = c.SortOrder,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                NoteCount = noteCounts.GetValueOrDefault(c.Id, 0),
                Children = BuildTree(flat, c.Id, noteCounts)
            })
            .ToList();
    }

    private static CategoryResponse ToResponse(Category c, int noteCount) => new()
    {
        Id = c.Id,
        NotebookId = c.NotebookId,
        ParentCategoryId = c.ParentCategoryId,
        Name = c.Name,
        SortOrder = c.SortOrder,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        NoteCount = noteCount,
        Children = new()
    };
}
