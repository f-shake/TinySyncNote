using TinySyncNote.Core.Models.Enums;

namespace TinySyncNote.Core.Models.Entities;

public class NoteConflict
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public int LocalVersion { get; set; }
    public int RemoteVersion { get; set; }
    public string LocalContent { get; set; } = string.Empty;
    public string RemoteContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public ConflictResolutionStrategy? ResolutionStrategy { get; set; }

    // Navigation
    public Note Note { get; set; } = null!;
}
