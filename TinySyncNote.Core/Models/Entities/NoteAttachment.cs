namespace TinySyncNote.Core.Models.Entities;

public class NoteAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? NoteId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Note? Note { get; set; }
}
