using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Services;

public interface IImportExportService
{
    Task<ExportNoteResult> ExportNoteAsync(Guid noteId, Guid userId);
    Task<ExportNoteResult> ExportNoteWithEmbeddedAssetsAsync(Guid noteId, Guid userId);
    Task<byte[]> ExportNoteAsZipAsync(Guid noteId, Guid userId);
    Task<ExportNoteHtmlResult> ExportNoteAsHtmlAsync(Guid noteId, Guid userId, string theme = "light");
    Task<byte[]> ExportNoteHtmlAsZipAsync(Guid noteId, Guid userId, string theme = "light");
    Task<byte[]> ExportNotebookAsync(Guid notebookId, Guid userId);
    Task<ImportResult> ImportMarkdownAsync(Guid categoryId, Guid userId, string fileName, Stream content);
    Task<ImportResult> ImportZipAsync(Guid notebookId, Guid userId, Stream zipStream);
}

public class ImportExportService : IImportExportService
{
    private readonly AppDbContext _db;

    /// <summary>匹配笔记内容中的 /api/attachment/{guid} 引用</summary>
    private static readonly Regex AttachmentUrlRx = new(
        @"/api/attachment/([0-9a-f\-]{36})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>匹配导出文件中的 {名称}.assets/{filename} 引用</summary>
    private static readonly Regex LocalAssetRx = new(
        @"([^""'\s\)\(]+\.assets/[^""'\s\)]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ImportExportService(AppDbContext db) => _db = db;

    // ── 单篇笔记 → Markdown（无 YAML 头部） ──
    public async Task<ExportNoteResult> ExportNoteAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        await VerifyNotebookAccess(note.Category.NotebookId, userId);

        var safeName = SanitizeFileName(note.Title);

        return new ExportNoteResult
        {
            FileName = $"{safeName}.md",
            Content = note.Content,
            ContentType = "text/markdown"
        };
    }

    // ── 单篇笔记 → Markdown（图片内嵌 base64） ──
    public async Task<ExportNoteResult> ExportNoteWithEmbeddedAssetsAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        await VerifyNotebookAccess(note.Category.NotebookId, userId);

        // 扫描附件引用，替换为 data: URI
        var guids = new HashSet<Guid>();
        foreach (Match m in AttachmentUrlRx.Matches(note.Content))
        {
            if (Guid.TryParse(m.Groups[1].Value, out var g))
                guids.Add(g);
        }

        var attachments = await _db.NoteAttachments
            .Where(a => guids.Contains(a.Id))
            .ToListAsync();
        var attachmentMap = attachments.ToDictionary(a => a.Id);

        var content = AttachmentUrlRx.Replace(note.Content, m =>
        {
            if (!Guid.TryParse(m.Groups[1].Value, out var guid)) return m.Value;
            if (!attachmentMap.TryGetValue(guid, out var att)) return m.Value;
            var b64 = Convert.ToBase64String(att.Data);
            return $"data:{att.ContentType};base64,{b64}";
        });

        var safeName = SanitizeFileName(note.Title);

        return new ExportNoteResult
        {
            FileName = $"{safeName}.md",
            Content = content,
            ContentType = "text/markdown"
        };
    }

    // ── 单篇笔记 → ZIP（含附件） ──
    public async Task<byte[]> ExportNoteAsZipAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Category)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        await VerifyNotebookAccess(note.Category.NotebookId, userId);

        // 扫描附件引用
        var guids = new HashSet<Guid>();
        foreach (Match m in AttachmentUrlRx.Matches(note.Content))
        {
            if (Guid.TryParse(m.Groups[1].Value, out var g))
                guids.Add(g);
        }

        var attachments = await _db.NoteAttachments
            .Where(a => guids.Contains(a.Id))
            .ToListAsync();
        var attachmentMap = attachments.ToDictionary(a => a.Id);

        var safeName = SanitizeFileName(note.Title);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var assetDir = $"{safeName}.assets";

            // 替换附件 URL 为 {safeName}.assets/ 本地路径
            var content = ReplaceAttachmentUrls(note.Content, attachmentMap, assetDir, out var usedGuids);

            // 写入 .md
            var entry = archive.CreateEntry($"{safeName}.md");
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                await writer.WriteAsync(content);
            }

            // 写入附件
            foreach (var guid in usedGuids)
            {
                if (!attachmentMap.TryGetValue(guid, out var att)) continue;
                var ext = Path.GetExtension(att.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".bin";

                var assetEntry = archive.CreateEntry($"{assetDir}/{guid}{ext}");
                using var assetStream = assetEntry.Open();
                assetStream.Write(att.Data);
            }
        }

        return ms.ToArray();
    }

    // ── 单篇笔记 → 渲染 HTML（图片内嵌 base64） ──
    public async Task<ExportNoteHtmlResult> ExportNoteAsHtmlAsync(Guid noteId, Guid userId, string theme = "light")
    {
        var note = await _db.Notes
            .Include(n => n.Category)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        await VerifyNotebookAccess(note.Category.NotebookId, userId);

        // 扫描附件引用，将图片内嵌为 base64
        var guids = new HashSet<Guid>();
        foreach (Match m in AttachmentUrlRx.Matches(note.Content))
        {
            if (Guid.TryParse(m.Groups[1].Value, out var g))
                guids.Add(g);
        }

        var attachments = await _db.NoteAttachments
            .Where(a => guids.Contains(a.Id))
            .ToListAsync();
        var attachmentMap = attachments.ToDictionary(a => a.Id);

        // 替换附件 URL 为 data: URI
        var htmlContent = AttachmentUrlRx.Replace(note.Content, m =>
        {
            if (!Guid.TryParse(m.Groups[1].Value, out var guid)) return m.Value;
            if (!attachmentMap.TryGetValue(guid, out var att)) return m.Value;
            var b64 = Convert.ToBase64String(att.Data);
            return $"data:{att.ContentType};base64,{b64}";
        });

        var safeName = SanitizeFileName(note.Title);
        var pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var bodyHtml = Markdig.Markdown.ToHtml(htmlContent, pipeline);

        var isDark = theme == "dark";
        var bg = isDark ? "#1a1a1a" : "#fff";
        var fg = isDark ? "#e0e0e0" : "#333";
        var codeBg = isDark ? "#2a2a2a" : "#f4f4f4";
        var border = isDark ? "#444" : "#ddd";
        var blockquoteColor = isDark ? "#aaa" : "#666";

        var html = $"<!DOCTYPE html>\n<html lang=\"zh-CN\">\n<head>\n" +
            $"<meta charset=\"UTF-8\">\n<title>{EscapeHtml(note.Title)}</title>\n" +
            $"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
            $"<style>\n" +
            $"body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; " +
            $"max-width: 800px; margin: 0 auto; padding: 20px; color: {fg}; background: {bg}; line-height: 1.6; }}\n" +
            $"h1, h2, h3, h4, h5, h6 {{ margin-top: 1.5em; margin-bottom: 0.5em; }}\n" +
            $"code {{ background: {codeBg}; padding: 2px 6px; border-radius: 3px; font-size: 0.9em; }}\n" +
            $"pre {{ background: {codeBg}; padding: 12px; border-radius: 6px; overflow-x: auto; }}\n" +
            $"pre code {{ background: none; padding: 0; }}\n" +
            $"blockquote {{ border-left: 4px solid {border}; margin: 0; padding: 0 16px; color: {blockquoteColor}; }}\n" +
            $"table {{ border-collapse: collapse; width: 100%; }}\n" +
            $"th, td {{ border: 1px solid {border}; padding: 8px 12px; text-align: left; }}\n" +
            $"th {{ background: {codeBg}; }}\n" +
            $"img {{ max-width: 100%; }}\n" +
            $"</style>\n</head>\n<body>\n" +
            $"<article class=\"markdown-body\">{bodyHtml}</article>\n</body>\n</html>";

        return new ExportNoteHtmlResult
        {
            FileName = $"{safeName}.html",
            Content = html
        };
    }

    // ── 单篇笔记 → HTML + assets/ ZIP ──
    public async Task<byte[]> ExportNoteHtmlAsZipAsync(Guid noteId, Guid userId, string theme = "light")
    {
        var note = await _db.Notes
            .Include(n => n.Category)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException("笔记不存在");

        await VerifyNotebookAccess(note.Category.NotebookId, userId);

        // 扫描附件引用
        var guids = new HashSet<Guid>();
        foreach (Match m in AttachmentUrlRx.Matches(note.Content))
        {
            if (Guid.TryParse(m.Groups[1].Value, out var g))
                guids.Add(g);
        }

        var attachments = await _db.NoteAttachments
            .Where(a => guids.Contains(a.Id))
            .ToListAsync();
        var attachmentMap = attachments.ToDictionary(a => a.Id);

        var safeName = SanitizeFileName(note.Title);

        // 替换附件 URL 为 {safeName}.assets/ 相对路径
        var mdContent = ReplaceAttachmentUrls(note.Content, attachmentMap, $"{safeName}.assets", out var usedGuids);

        var pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var bodyHtml = Markdig.Markdown.ToHtml(mdContent, pipeline);

        var isDark = theme == "dark";
        var bg = isDark ? "#1a1a1a" : "#fff";
        var fg = isDark ? "#e0e0e0" : "#333";
        var codeBg = isDark ? "#2a2a2a" : "#f4f4f4";
        var border = isDark ? "#444" : "#ddd";
        var blockquoteColor = isDark ? "#aaa" : "#666";

        var html = $"<!DOCTYPE html>\n<html lang=\"zh-CN\">\n<head>\n" +
            $"<meta charset=\"UTF-8\">\n<title>{EscapeHtml(note.Title)}</title>\n" +
            $"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
            $"<style>\n" +
            $"body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; " +
            $"max-width: 800px; margin: 0 auto; padding: 20px; color: {fg}; background: {bg}; line-height: 1.6; }}\n" +
            $"h1, h2, h3, h4, h5, h6 {{ margin-top: 1.5em; margin-bottom: 0.5em; }}\n" +
            $"code {{ background: {codeBg}; padding: 2px 6px; border-radius: 3px; font-size: 0.9em; }}\n" +
            $"pre {{ background: {codeBg}; padding: 12px; border-radius: 6px; overflow-x: auto; }}\n" +
            $"pre code {{ background: none; padding: 0; }}\n" +
            $"blockquote {{ border-left: 4px solid {border}; margin: 0; padding: 0 16px; color: {blockquoteColor}; }}\n" +
            $"table {{ border-collapse: collapse; width: 100%; }}\n" +
            $"th, td {{ border: 1px solid {border}; padding: 8px 12px; text-align: left; }}\n" +
            $"th {{ background: {codeBg}; }}\n" +
            $"img {{ max-width: 100%; }}\n" +
            $"</style>\n</head>\n<body>\n" +
            $"<article class=\"markdown-body\">{bodyHtml}</article>\n</body>\n</html>";

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var assetDir = $"{safeName}.assets";

            // 写入 .html
            var entry = archive.CreateEntry($"{safeName}.html");
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                await writer.WriteAsync(html);
            }

            // 写入附件
            foreach (var guid in usedGuids)
            {
                if (!attachmentMap.TryGetValue(guid, out var att)) continue;
                var ext = Path.GetExtension(att.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".bin";

                var assetEntry = archive.CreateEntry($"{assetDir}/{guid}{ext}");
                using var assetStream = assetEntry.Open();
                assetStream.Write(att.Data);
            }
        }

        return ms.ToArray();
    }

    // ── 整个笔记本 → ZIP（含附件 assets/） ──
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

        // 收集所有笔记中引用的附件 GUID
        var allGuids = new HashSet<Guid>();
        foreach (var note in notes)
        {
            foreach (Match m in AttachmentUrlRx.Matches(note.Content))
            {
                if (Guid.TryParse(m.Groups[1].Value, out var g))
                    allGuids.Add(g);
            }
        }

        // 从数据库加载所有引用的附件
        var attachments = await _db.NoteAttachments
            .Where(a => allGuids.Contains(a.Id))
            .ToListAsync();
        var attachmentMap = attachments.ToDictionary(a => a.Id);

        // 第一遍：统计目录路径出现次数，同名目录全部编号从 1 开始
        var rawPaths = categories.ToDictionary(c => c.Id, c => BuildRawCategoryPath(c, categories));
        var pathTotal = new Dictionary<string, int>();
        foreach (var raw in rawPaths.Values)
            pathTotal[raw] = pathTotal.GetValueOrDefault(raw) + 1;

        var catPathLookup = new Dictionary<Guid, string>();
        var pathIdx = new Dictionary<string, int>();
        foreach (var cat in categories)
        {
            var raw = rawPaths[cat.Id];
            if (pathTotal[raw] > 1)
            {
                pathIdx[raw] = pathIdx.GetValueOrDefault(raw) + 1;
                catPathLookup[cat.Id] = $"{raw} ({pathIdx[raw]})";
            }
            else
            {
                catPathLookup[cat.Id] = raw;
            }
        }

        // 第二遍：统计每个目录下笔记标题出现次数
        var titleTotal = new Dictionary<string, Dictionary<string, int>>();
        foreach (var note in notes)
        {
            var catPath = catPathLookup[note.CategoryId];
            var safeName = SanitizeFileName(note.Title);
            if (!titleTotal.TryGetValue(catPath, out var dirTotals))
                titleTotal[catPath] = dirTotals = [];
            dirTotals[safeName] = dirTotals.GetValueOrDefault(safeName) + 1;
        }

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var titleIdx = new Dictionary<string, Dictionary<string, int>>();
            // 按笔记路径追踪引用的附件：catPath → safeName → [guids]
            var catNoteAssets = new Dictionary<string, Dictionary<string, HashSet<Guid>>>();

            foreach (var note in notes)
            {
                var catPath = catPathLookup[note.CategoryId];
                var safeName = SanitizeFileName(note.Title);

                string entryName;
                if (titleTotal[catPath][safeName] > 1)
                {
                    if (!titleIdx.TryGetValue(catPath, out var dirIdx))
                        titleIdx[catPath] = dirIdx = [];
                    dirIdx[safeName] = dirIdx.GetValueOrDefault(safeName) + 1;
                    var idx = dirIdx[safeName];
                    entryName = string.IsNullOrEmpty(catPath)
                        ? $"{safeName} ({idx}).md"
                        : $"{catPath}/{safeName} ({idx}).md";
                }
                else
                {
                    entryName = string.IsNullOrEmpty(catPath)
                        ? $"{safeName}.md"
                        : $"{catPath}/{safeName}.md";
                }

                var assetDir = $"{safeName}.assets";

                // 替换附件 URL 为 {safeName}.assets/ 相对路径
                var content = ReplaceAttachmentUrls(note.Content, attachmentMap, assetDir, out var usedGuids);

                // 追踪该笔记下被引用的附件
                if (!catNoteAssets.TryGetValue(catPath, out var catDict))
                    catNoteAssets[catPath] = catDict = [];
                if (!catDict.TryGetValue(safeName, out var noteGuids))
                    catDict[safeName] = noteGuids = [];
                noteGuids.UnionWith(usedGuids);

                var entry = archive.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                await writer.WriteAsync(content);
            }

            // 写入附件 → {catPath}/{safeName}.assets/{guid}.ext
            foreach (var (catPath, catDict) in catNoteAssets)
            {
                foreach (var (safeName, guids) in catDict)
                {
                    foreach (var guid in guids)
                    {
                        if (!attachmentMap.TryGetValue(guid, out var att)) continue;
                        var ext = Path.GetExtension(att.FileName);
                        if (string.IsNullOrEmpty(ext)) ext = ".bin";

                        var assetPath = string.IsNullOrEmpty(catPath)
                            ? $"{safeName}.assets/{guid}{ext}"
                            : $"{catPath}/{safeName}.assets/{guid}{ext}";

                        var assetEntry = archive.CreateEntry(assetPath);
                        using var assetStream = assetEntry.Open();
                        assetStream.Write(att.Data);
                    }
                }
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

    // ── 导入 ZIP（含 assets/ 附件支持） ──
    public async Task<ImportResult> ImportZipAsync(Guid notebookId, Guid userId, Stream zipStream)
    {
        var result = new ImportResult();
        ClearCategoryCache();

        var notebook = await _db.Notebooks
            .FirstOrDefaultAsync(n => n.Id == notebookId && n.UserId == userId)
            ?? throw new KeyNotFoundException("笔记本不存在");

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // 跨多个笔记共享的附件导入缓存（old asset name → new /api/attachment/{guid}）
        var importedAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // 目录条目
            if (!entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue; // 只处理 .md

            try
            {
                // 解析目录路径（.md 所在的目录）
                var dirPath = Path.GetDirectoryName(entry.FullName)?.Replace('\\', '/') ?? "";
                var category = await GetOrCreateCategory(notebookId, dirPath);

                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                var text = await reader.ReadToEndAsync();

                // 替换本地 {名称}.assets/ 引用为 /api/attachment/{guid}
                // 资产文件位于 .md 同目录下的 {名称}.assets/ 中
                text = LocalAssetRx.Replace(text, m =>
                {
                    var fullRef = m.Groups[1].Value; // e.g., "Note 1.assets/guid.png"

                    // 已导入的附件直接复用
                    if (importedAssets.TryGetValue(fullRef, out var existingUrl))
                        return existingUrl;

                    // 在 ZIP 中查找文件：{dirPath}/{fullRef}
                    var fullAssetPath = string.IsNullOrEmpty(dirPath)
                        ? fullRef
                        : $"{dirPath}/{fullRef}";

                    var assetEntry = archive.Entries
                        .FirstOrDefault(e =>
                            string.Equals(e.FullName.Replace('\\', '/'), fullAssetPath, StringComparison.OrdinalIgnoreCase));

                    if (assetEntry == null)
                        return m.Value; // 找不到则保持原样

                    // 读取附件二进制
                    byte[] data;
                    using (var assetStream = assetEntry.Open())
                    using (var ms = new MemoryStream())
                    {
                        assetStream.CopyTo(ms);
                        data = ms.ToArray();
                    }

                    var contentType = GetContentType(Path.GetExtension(fullRef));

                    var attachment = new NoteAttachment
                    {
                        FileName = fullRef,
                        ContentType = contentType,
                        Data = data,
                        FileSize = data.Length
                    };
                    _db.NoteAttachments.Add(attachment);

                    var newUrl = $"/api/attachment/{attachment.Id}";
                    importedAssets[fullRef] = newUrl;
                    return newUrl;
                });

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

    /// <summary>替换笔记内容中的 /api/attachment/{guid} 为 {assetDir}/{guid}{ext}</summary>
    private static string ReplaceAttachmentUrls(
        string content,
        Dictionary<Guid, NoteAttachment> attachmentMap,
        string assetDir,
        out HashSet<Guid> usedGuids)
    {
        var used = new HashSet<Guid>();
        var result = AttachmentUrlRx.Replace(content, m =>
        {
            if (!Guid.TryParse(m.Groups[1].Value, out var guid)) return m.Value;
            if (!attachmentMap.TryGetValue(guid, out var att)) return m.Value;

            var ext = Path.GetExtension(att.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".bin";
            used.Add(guid);
            return $"{assetDir}/{guid}{ext}";
        });
        usedGuids = used;
        return result;
    }

    /// <summary>根据扩展名获取 Content-Type</summary>
    private static string GetContentType(string ext) => ext switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };

    private static string EscapeHtml(string text)
        => System.Net.WebUtility.HtmlEncode(text);

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
        // 内存缓存：避免同一批次内重复查 DB（新创建的 category 尚未持久化）
        if (!_catCache.TryGetValue(notebookId, out var nbCache))
            _catCache[notebookId] = nbCache = [];

        var key = $"{notebookId}:{path}";
        if (nbCache.TryGetValue(key, out var cached))
            return cached;

        if (string.IsNullOrEmpty(path))
        {
            // 使用根目录（第一个或创建）
            var root = await _db.Categories
                .FirstOrDefaultAsync(c => c.NotebookId == notebookId && c.ParentCategoryId == null);
            if (root != null)
            {
                nbCache[key] = root;
                return root;
            }

            root = new Category { NotebookId = notebookId, Name = "导入", SortOrder = 0 };
            _db.Categories.Add(root);
            nbCache[key] = root;
            return root;
        }

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Guid? parentId = null;
        Category? current = null;

        foreach (var part in parts)
        {
            var catName = StripDedupSuffix(part);
            current = await _db.Categories
                .FirstOrDefaultAsync(c => c.NotebookId == notebookId
                                       && c.ParentCategoryId == parentId
                                       && c.Name == catName);
            if (current == null)
            {
                current = new Category
                {
                    NotebookId = notebookId,
                    ParentCategoryId = parentId,
                    Name = catName,
                    SortOrder = 0
                };
                _db.Categories.Add(current);
            }
            parentId = current.Id;
        }

        nbCache[key] = current!;
        return current!;
    }

    // 导入过程中用于缓存 category 查找结果，避免同批导入重复创建同名目录
    private readonly Dictionary<Guid, Dictionary<string, Category>> _catCache = [];

    private void ClearCategoryCache() => _catCache.Clear();

    private static string BuildRawCategoryPath(Category category, List<Category> allCategories)
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

    /// <summary>脱掉导出时追加的 " (N)" 编号后缀</summary>
    private static string StripDedupSuffix(string name)
    {
        if (name.Length < 4 || name[^1] != ')') return name;
        var parenIdx = name.LastIndexOf(" (", StringComparison.Ordinal);
        if (parenIdx < 0) return name;
        if (int.TryParse(name[(parenIdx + 2)..^1], out _))
            return name[..parenIdx];
        return name;
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
