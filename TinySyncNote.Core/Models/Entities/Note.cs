namespace TinySyncNote.Core.Models.Entities;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int FormatVersion { get; set; } = 1;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Category Category { get; set; } = null!;
    public ICollection<NoteSnapshot> Snapshots { get; set; } = new List<NoteSnapshot>();
    public ICollection<NoteConflict> Conflicts { get; set; } = new List<NoteConflict>();
}
