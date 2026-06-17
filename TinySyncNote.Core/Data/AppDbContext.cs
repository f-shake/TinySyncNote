using Microsoft.EntityFrameworkCore;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Notebook> Notebooks => Set<Notebook>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteSnapshot> NoteSnapshots => Set<NoteSnapshot>();
    public DbSet<NoteConflict> NoteConflicts => Set<NoteConflict>();
    public DbSet<SyncTracking> SyncTrackings => Set<SyncTracking>();
    public DbSet<NoteShare> NoteShares => Set<NoteShare>();
    public DbSet<PublicShare> PublicShares => Set<PublicShare>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<NoteAttachment> NoteAttachments => Set<NoteAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        // Notebook
        modelBuilder.Entity<Notebook>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IsSystem).HasDefaultValue(false);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Notebooks)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.IsSystem });
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.HasOne(e => e.Notebook)
                  .WithMany(n => n.Categories)
                  .HasForeignKey(e => e.NotebookId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ParentCategory)
                  .WithMany(c => c.ChildCategories)
                  .HasForeignKey(e => e.ParentCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Note
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Notes)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // NoteSnapshot
        modelBuilder.Entity<NoteSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.Note)
                  .WithMany(n => n.Snapshots)
                  .HasForeignKey(e => e.NoteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // NoteConflict
        modelBuilder.Entity<NoteConflict>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Note)
                  .WithMany(n => n.Conflicts)
                  .HasForeignKey(e => e.NoteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SyncTracking
        modelBuilder.Entity<SyncTracking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        // NoteShare
        modelBuilder.Entity<NoteShare>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Note)
                  .WithMany()
                  .HasForeignKey(e => e.NoteId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.NoteId, e.SharedWithUserId }).IsUnique();
        });

        // PublicShare
        modelBuilder.Entity<PublicShare>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.Token).HasMaxLength(128).IsRequired();
            entity.HasOne(e => e.Note)
                  .WithMany()
                  .HasForeignKey(e => e.NoteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // UserSetting
        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(50).IsRequired();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.Key }).IsUnique();
        });

        // NoteAttachment
        modelBuilder.Entity<NoteAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Data).IsRequired();
            entity.HasOne(e => e.Note)
                  .WithMany()
                  .HasForeignKey(e => e.NoteId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.NoteId);
        });
    }
}
