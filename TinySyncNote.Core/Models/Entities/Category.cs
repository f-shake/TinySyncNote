namespace TinySyncNote.Core.Models.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotebookId { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Notebook Notebook { get; set; } = null!;
    public Category? ParentCategory { get; set; }
    public ICollection<Category> ChildCategories { get; set; } = new List<Category>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
