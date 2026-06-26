namespace TinySyncNote.Core.Models.DTOs;

public class ExportNoteResult
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/markdown";
}

public class ExportNoteHtmlResult
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/html";
}

public class ExportNoteBinaryResult
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}

public class ImportResult
{
    public int NotesImported { get; set; }
    public List<string> Errors { get; set; } = new();
}
