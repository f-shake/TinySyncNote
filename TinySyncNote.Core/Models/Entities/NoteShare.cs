namespace TinySyncNote.Core.Models.Entities;

public class NoteShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid SharedWithUserId { get; set; }
    public Guid SharedNoteCopyId { get; set; }
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Note Note { get; set; } = null!;
}
