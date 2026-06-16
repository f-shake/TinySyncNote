namespace TinySyncNote.Core.Models.Entities;

public class UserSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
