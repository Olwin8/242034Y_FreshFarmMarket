using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using _242034Y_FreshFarmMarket.Models;

namespace _242034Y_FreshFarmMarket.Models
{
    public class AuthDbContext : IdentityDbContext<ApplicationUser>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // ✅ NEW: Password history
        public DbSet<PasswordHistory> PasswordHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure unique constraint for Email
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure UserSession
            builder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.SessionId);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.SessionId)
                    .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.LastActivity)
                    .HasDefaultValueSql("GETDATE()");

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure AuditLog
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Success);

                // ✅ FIX: optional FK (UserId can be null)
                entity.HasOne(a => a.User)
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ApplicationUser table
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasIndex(e => e.CreatedDate);
                entity.HasIndex(e => e.LastLoginDate);
                entity.HasIndex(e => e.FailedLoginAttempts);

                // ✅ NEW: password age policy support
                entity.HasIndex(e => e.LastPasswordChangeDate);
            });

            // ✅ NEW: PasswordHistory
            builder.Entity<PasswordHistory>(entity =>
            {
                entity.HasIndex(p => p.UserId);
                entity.HasIndex(p => p.ChangedAt);

                entity.Property(p => p.PasswordHash)
                      .HasMaxLength(500);
            });
        }
    }
}
