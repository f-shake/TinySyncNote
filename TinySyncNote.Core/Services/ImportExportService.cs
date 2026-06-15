using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Services;

public interface IImportExportService
{
    Task<ExportNoteResult> ExportNoteAsync(Guid noteId, Guid userId);
    Task<byte[]> ExportNotebookAsync(Guid notebookId, Guid userId);
    Task<ImportResult> ImportMarkdownAsync(Guid categoryId, Guid userId, string fileName, Stream content);
    Task<ImportResult> ImportZipAsync(Guid notebookId, Guid userId, Stream zipStream);
}

public class ImportExportService : IImportExportService
{
    private readonly AppDbContext _db;

    public ImportExportService(AppDbContext db) => _db = db;

    // ── 单篇笔记 → Markdown ──
    public async Task<ExportNoteResult> ExportNoteAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        await VerifyNotebookAccess(note.Category.NotebookId, userId);

        var safeName = SanitizeFileName(note.Title);
        var md = BuildMarkdownWithFrontMatter(note.Title, note.Content, note.CreatedAt, note.UpdatedAt, note.Version);

        return new ExportNoteResult
        {
            FileName = $"{safeName}.md",
            Content = md,
            ContentType = "text/markdown"
        };
    }

    // ── 整个笔记本 → ZIP ──
    public async Task<byte[]> ExportNotebookAsync(Guid notebookId, Guid userId)
    {
        var notebook = await _db.Notebooks
            .FirstOrDefaultAsync(n => n.Id == notebookId && n.UserId == userId)
            ?? throw new KeyNotFoundException("笔记本不存在");

        var categories = await _db.Categories
            .Where(c => c.NotebookId == notebookId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var notes = await _db.Notes
            .Where(n => n.Category.NotebookId == notebookId)
            .Include(n => n.Category)
            .ToListAsync();

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var note in notes)
            {
                var catPath = BuildCategoryPath(note.Category, categories);
                var safeName = SanitizeFileName(note.Title);
                var entryName = string.IsNullOrEmpty(catPath)
                    ? $"{safeName}.md"
                    : $"{catPath}/{safeName}.md";

                var entry = archive.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                await writer.WriteAsync(BuildMarkdownWithFrontMatter(
                    note.Title, note.Content, note.CreatedAt, note.UpdatedAt, note.Version));
            }
        }

        return ms.ToArray();
    }

    // ── 导入单个 Markdown ──
    public async Task<ImportResult> ImportMarkdownAsync(Guid categoryId, Guid userId, string fileName, Stream content)
    {
        var result = new ImportResult();

        try
        {
            await VerifyCategoryAccess(categoryId, userId);

            using var reader = new StreamReader(content, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();

            var (title, body) = ParseMarkdown(text, Path.GetFileNameWithoutExtension(fileName));

            var note = new Note
            {
                CategoryId = categoryId,
                Title = title,
                Content = body,
                Version = 1
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();
            result.NotesImported = 1;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"{fileName}: {ex.Message}");
        }

        return result;
    }

    // ── 导入 ZIP ──
    public async Task<ImportResult> ImportZipAsync(Guid notebookId, Guid userId, Stream zipStream)
    {
        var result = new ImportResult();

        var notebook = await _db.Notebooks
            .FirstOrDefaultAsync(n => n.Id == notebookId && n.UserId == userId)
            ?? throw new KeyNotFoundException("笔记本不存在");

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // 目录条目

            try
            {
                // 解析目录路径
                var dirPath = Path.GetDirectoryName(entry.FullName)?.Replace('\\', '/') ?? "";
                var category = await GetOrCreateCategory(notebookId, dirPath);

                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                var text = await reader.ReadToEndAsync();

                var (title, body) = ParseMarkdown(text, Path.GetFileNameWithoutExtension(entry.Name));

                var note = new Note
                {
                    CategoryId = category.Id,
                    Title = title,
                    Content = body,
                    Version = 1
                };

                _db.Notes.Add(note);
                result.NotesImported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{entry.FullName}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    // ── 辅助方法 ──

    private string BuildMarkdownWithFrontMatter(string title, string content,
        DateTime createdAt, DateTime updatedAt, int version)
    {
        return $"---\n" +
               $"title: \"{title.Replace("\"", "\\\"")}\"\n" +
               $"created_at: {createdAt:O}\n" +
               $"updated_at: {updatedAt:O}\n" +
               $"version: {version}\n" +
               $"---\n\n" +
               $"{content}";
    }

    private (string title, string body) ParseMarkdown(string text, string defaultTitle)
    {
        // 尝试解析 YAML front-matter
        if (text.StartsWith("---"))
        {
            var endIdx = text.IndexOf("---", 3, StringComparison.Ordinal);
            if (endIdx > 0)
            {
                var frontMatter = text[3..endIdx].Trim();
                var body = text[(endIdx + 3)..].TrimStart();

                // 提取 title
                foreach (var line in frontMatter.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("title:"))
                    {
                        var t = trimmed["title:".Length..].Trim().Trim('"', '\'');
                        return (t, body);
                    }
                }

                return (defaultTitle, body);
            }
        }

        return (defaultTitle, text);
    }

    private async Task<Category> GetOrCreateCategory(Guid notebookId, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            // 使用根目录（第一个或创建）
            var root = await _db.Categories
                .FirstOrDefaultAsync(c => c.NotebookId == notebookId && c.ParentCategoryId == null);
            if (root != null) return root;

            root = new Category
            {
                NotebookId = notebookId,
                Name = "导入",
                SortOrder = 0
            };
            _db.Categories.Add(root);
            return root;
        }

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Guid? parentId = null;
        Category? current = null;

        foreach (var part in parts)
        {
            current = await _db.Categories
                .FirstOrDefaultAsync(c => c.NotebookId == notebookId
                                       && c.ParentCategoryId == parentId
                                       && c.Name == part);
            if (current == null)
            {
                current = new Category
                {
                    NotebookId = notebookId,
                    ParentCategoryId = parentId,
                    Name = part,
                    SortOrder = 0
                };
                _db.Categories.Add(current);
            }
            parentId = current.Id;
        }

        return current!;
    }

    private static string BuildCategoryPath(Category category, List<Category> allCategories)
    {
        var parts = new List<string>();
        var current = category;
        while (current != null)
        {
            parts.Add(SanitizeFileName(current.Name));
            current = allCategories.FirstOrDefault(c => c.Id == current.ParentCategoryId);
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized.Trim();
    }

    private async Task VerifyNotebookAccess(Guid notebookId, Guid userId)
    {
        var exists = await _db.Notebooks.AnyAsync(n => n.Id == notebookId && n.UserId == userId);
        if (!exists) throw new UnauthorizedAccessException("无权访问");
    }

    private async Task VerifyCategoryAccess(Guid categoryId, Guid userId)
    {
        var hasAccess = await _db.Categories
            .Include(c => c.Notebook)
            .AnyAsync(c => c.Id == categoryId && c.Notebook.UserId == userId);
        if (!hasAccess) throw new KeyNotFoundException("目录不存在");
    }
}
