using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FinPulse.Domain.Common;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FinPulse.Domain.Exceptions;

namespace FinPulse.Infrastructure.Persistence;

public class FinPulseDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public FinPulseDbContext(DbContextOptions<FinPulseDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.HasIndex(u => u.Email).IsUnique();
            b.Property(u => u.Email).HasMaxLength(256).IsRequired();
            b.Property(u => u.FullName).HasMaxLength(256).IsRequired();
            b.Property(u => u.Role).HasMaxLength(50).IsRequired();
        });

        // Wallet Configuration
        modelBuilder.Entity<Wallet>(b =>
        {
            b.HasKey(w => w.Id);
            b.HasIndex(w => w.AccountNumber).IsUnique();
            b.HasIndex(w => w.UserId);
            b.Property(w => w.AccountNumber).HasMaxLength(50).IsRequired();
            b.Property(w => w.Balance).HasPrecision(18, 2).IsRequired();
            
            // Optimistic Concurrency Control Token
            b.Property(w => w.ConcurrencyToken).IsConcurrencyToken().IsRequired();

            b.HasOne(w => w.User)
             .WithMany(u => u.Wallets)
             .HasForeignKey(w => w.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Transaction Configuration
        modelBuilder.Entity<Transaction>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => t.ReferenceNumber).IsUnique();
            b.HasIndex(t => t.SourceWalletId);
            b.HasIndex(t => t.DestinationWalletId);
            b.Property(t => t.ReferenceNumber).HasMaxLength(50).IsRequired();
            b.Property(t => t.Amount).HasPrecision(18, 2).IsRequired();
            b.Property(t => t.Description).HasMaxLength(500);

            b.HasOne(t => t.SourceWallet)
             .WithMany()
             .HasForeignKey(t => t.SourceWalletId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.DestinationWallet)
             .WithMany()
             .HasForeignKey(t => t.DestinationWalletId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog Configuration
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.EntityId);
            b.HasIndex(a => a.TimestampUtc);
            b.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
            b.Property(a => a.PerformedBy).HasMaxLength(100);
        });

        // IdempotencyRecord Configuration
        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.HasKey(i => i.Key);
            b.Property(i => i.RequestHash).HasMaxLength(128).IsRequired();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Automatic Auditing for modified or added entities
        var auditEntries = PrepareAuditEntries();

        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);

            // 2. Save audit logs if any were collected
            if (auditEntries.Count != 0)
            {
                AuditLogs.AddRange(auditEntries);
                await base.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "A concurrency conflict occurred. The wallet balance or status was modified by another operation. Please retry.");
        }
    }

    private List<AuditLog> PrepareAuditEntries()
    {
        ChangeTracker.DetectChanges();
        var auditLogs = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.Entity is IdempotencyRecord || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var entityName = entry.Entity.GetType().Name;
            var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? Guid.NewGuid().ToString();

            var changes = new Dictionary<string, object?>();
            foreach (var property in entry.Properties)
            {
                if (property.IsModified || entry.State == EntityState.Added)
                {
                    changes[property.Metadata.Name] = new
                    {
                        Original = entry.State == EntityState.Added ? null : property.OriginalValue,
                        Current = property.CurrentValue
                    };
                }
            }

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated
            };

            var changesJson = JsonSerializer.Serialize(changes);
            var log = new AuditLog(Guid.NewGuid(), entityName, primaryKey, action, "System", changesJson);
            auditLogs.Add(log);
        }

        return auditLogs;
    }
}
