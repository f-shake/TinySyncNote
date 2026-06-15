using System.ComponentModel.DataAnnotations;

namespace TinySyncNote.Core.Models.DTOs;

public class CreateCategoryRequest
{
    [Required]
    public Guid NotebookId { get; set; }

    public Guid? ParentCategoryId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}

public class UpdateCategoryRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    public int SortOrder { get; set; }
}

public class CategoryResponse
{
    public Guid Id { get; set; }
    public Guid NotebookId { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CategoryResponse> Children { get; set; } = new();
    public int NoteCount { get; set; }
}
