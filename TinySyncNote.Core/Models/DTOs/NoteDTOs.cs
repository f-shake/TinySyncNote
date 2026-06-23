using System.ComponentModel.DataAnnotations;

namespace TinySyncNote.Core.Models.DTOs;

public class CreateNoteRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public class UpdateNoteRequest
{
    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>乐观锁版本号</summary>
    public int Version { get; set; }
}

/// <summary>笔记详细（含正文）</summary>
public class NoteDetailResponse
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid NotebookId { get; set; }
    public string NotebookName { get; set; } = string.Empty;
}

/// <summary>笔记列表项（不含正文，用于列表展示）</summary>
public class NoteListItem
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime UpdatedAt { get; set; }
}
