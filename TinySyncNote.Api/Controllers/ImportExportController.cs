using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ImportExportController : ControllerBase
{
    private readonly IImportExportService _service;

    public ImportExportController(IImportExportService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    /// <summary>导出单篇笔记为 Markdown（无 YAML 头部）</summary>
    [HttpGet("note/{noteId}/markdown")]
    public async Task<ActionResult> ExportNote(Guid noteId)
    {
        try
        {
            var result = await _service.ExportNoteAsync(noteId, UserId);
            return FileWithChineseName(
                System.Text.Encoding.UTF8.GetBytes(result.Content),
                result.ContentType,
                result.FileName
            );
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>导出单篇笔记为渲染后的 HTML</summary>
    [HttpGet("note/{noteId}/html")]
    public async Task<ActionResult> ExportNoteHtml(Guid noteId, [FromQuery] string theme = "light")
    {
        try
        {
            var result = await _service.ExportNoteAsHtmlAsync(noteId, UserId, theme);
            return FileWithChineseName(
                System.Text.Encoding.UTF8.GetBytes(result.Content),
                result.ContentType,
                result.FileName
            );
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>支持中文文件名的 File 响应</summary>
    private ActionResult FileWithChineseName(byte[] bytes, string contentType, string fileName)
    {
        var encoded = Uri.EscapeDataString(fileName);
        Response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{encoded}";
        return File(bytes, contentType);
    }

    /// <summary>导出整个笔记本为 ZIP</summary>
    [HttpGet("notebook/{notebookId}")]
    public async Task<ActionResult> ExportNotebook(Guid notebookId)
    {
        try
        {
            var bytes = await _service.ExportNotebookAsync(notebookId, UserId);
            return File(bytes, "application/zip");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>导入 Markdown 文件</summary>
    [HttpPost("import/markdown")]
    public async Task<ActionResult<ImportResult>> ImportMarkdown(
        [FromQuery] Guid categoryId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择文件" });

        using var stream = file.OpenReadStream();
        var result = await _service.ImportMarkdownAsync(categoryId, UserId, file.FileName, stream);

        if (result.Errors.Count > 0)
            return Ok(new { result.NotesImported, Errors = result.Errors });

        return Ok(result);
    }

    /// <summary>导入 ZIP 压缩包</summary>
    [HttpPost("import/zip")]
    public async Task<ActionResult<ImportResult>> ImportZip(
        [FromQuery] Guid notebookId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择文件" });

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _service.ImportZipAsync(notebookId, UserId, stream);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
