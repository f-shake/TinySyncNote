namespace TinySyncNote.Core.Models.DTOs;

public class SnapshotResponse
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public string SnapshotType { get; set; } = "Automatic";
    public DateTime SnapshotAt { get; set; }
}

public class SnapshotListItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; }
    public string SnapshotType { get; set; } = "Automatic";
    public DateTime SnapshotAt { get; set; }
}
