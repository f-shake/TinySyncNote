namespace TinySyncNote.Core.Models.DTOs;

public class ConflictResponse
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string NoteTitle { get; set; } = string.Empty;
    public int LocalVersion { get; set; }
    public int RemoteVersion { get; set; }
    public string LocalContent { get; set; } = string.Empty;
    public string RemoteContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionStrategy { get; set; }
}

public class ConflictListItem
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string NoteTitle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ResolveConflictRequest
{
    /// <summary>KeepLocal | KeepRemote | Merged</summary>
    public string Strategy { get; set; } = "KeepLocal";

    /// <summary>当 Strategy 为 Merged 时的合并内容</summary>
    public string? MergedContent { get; set; }
}
