namespace TinySyncNote.Core.Models.Entities;

public class SyncTracking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty; // "Note", "Category", "Notebook"
    public Guid EntityId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public int ChangeVersion { get; set; }
    public string ChangedByDeviceId { get; set; } = string.Empty;
}
