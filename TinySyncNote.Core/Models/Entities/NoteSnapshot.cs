using TinySyncNote.Core.Models.Enums;

namespace TinySyncNote.Core.Models.Entities;

public class NoteSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public int FormatVersion { get; set; } = 1;
    public SnapshotType SnapshotType { get; set; } = SnapshotType.Automatic;
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Note Note { get; set; } = null!;
}
