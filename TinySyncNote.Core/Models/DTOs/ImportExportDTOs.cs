namespace TinySyncNote.Core.Models.DTOs;

public class ExportNoteResult
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/markdown";
}

public class ImportResult
{
    public int NotesImported { get; set; }
    public List<string> Errors { get; set; } = new();
}
