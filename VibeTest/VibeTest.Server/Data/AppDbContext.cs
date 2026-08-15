using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Models.Entities;

namespace VibeTest.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<UserTestResult> UserTestResults => Set<UserTestResult>();
    public DbSet<TestApplication> TestApplications => Set<TestApplication>();
    public DbSet<ApplicationResult> ApplicationResults => Set<ApplicationResult>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Test>(entity =>
        {
            entity.Property(t => t.Difficulty)
                .HasDefaultValue(TestDifficulty.Easy);

            entity.HasOne(t => t.Author)
                .WithMany(u => u.AuthoredTests)
                .HasForeignKey(t => t.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasIndex(q => new { q.TestId, q.Order }).IsUnique();

            entity.HasOne(q => q.Test)
                .WithMany(t => t.Questions)
                .HasForeignKey(q => q.TestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasIndex(a => new { a.QuestionId, a.Order }).IsUnique();

            entity.HasOne(a => a.Question)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserTestResult>(entity =>
        {
            entity.HasIndex(utr => new { utr.UserId, utr.TestId }).IsUnique();

            entity.HasOne(utr => utr.User)
                .WithMany(u => u.UserTestResults)
                .HasForeignKey(utr => utr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(utr => utr.Test)
                .WithMany(t => t.UserTestResults)
                .HasForeignKey(utr => utr.TestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity.HasIndex(r => new { r.UserId, r.TestId, r.QuestionId }).IsUnique();

            entity.HasOne(r => r.User)
                .WithMany(u => u.Results)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Test)
                .WithMany(t => t.Results)
                .HasForeignKey(r => r.TestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Question)
                .WithMany(q => q.Results)
                .HasForeignKey(r => r.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Answer)
                .WithMany(a => a.Results)
                .HasForeignKey(r => r.AnswerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TestApplication>(entity =>
        {
            entity.HasIndex(a => a.Token).IsUnique();
            entity.Property(a => a.HideResultsFromParticipant).HasDefaultValue(false);
            entity.Property(a => a.Type)
                .HasDefaultValue(ApplicationType.Link);

            entity.HasOne(a => a.Author)
                .WithMany()
                .HasForeignKey(a => a.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Test)
                .WithMany()
                .HasForeignKey(a => a.TestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(a => a.RecipientUserId);

            entity.HasOne(a => a.Recipient)
                .WithMany()
                .HasForeignKey(a => a.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationResult>(entity =>
        {
            entity.HasIndex(r => new { r.ApplicationId, r.QuestionId }).IsUnique();

            entity.HasOne(r => r.Application)
                .WithMany(a => a.Results)
                .HasForeignKey(r => r.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Question)
                .WithMany()
                .HasForeignKey(r => r.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Answer)
                .WithMany()
                .HasForeignKey(r => r.AnswerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(rt => rt.Token).IsUnique();

            entity.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
