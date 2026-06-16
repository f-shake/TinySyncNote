namespace TinySyncNote.Core.Models.DTOs;

public class ShareNoteRequest
{
    public Guid SharedWithUserId { get; set; }
}

public class NoteShareResponse
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string OwnerUsername { get; set; } = string.Empty;
    public Guid SharedWithUserId { get; set; }
    public Guid SharedNoteCopyId { get; set; }
    public DateTime SharedAt { get; set; }
}

public class PublicShareResponse
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string ShareUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}

public class PublicShareViewResponse
{
    public string Title { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreatePublicLinkRequest
{
    public DateTime? ExpiresAt { get; set; }
}

public class UserSearchResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
}
