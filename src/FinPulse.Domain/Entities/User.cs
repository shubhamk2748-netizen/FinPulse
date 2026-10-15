using FinPulse.Domain.Common;

namespace FinPulse.Domain.Entities;

public class User : Entity<Guid>, IAuditableEntity
{
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = "Customer"; // Customer or Admin
    public bool IsActive { get; private set; } = true;

    // Navigation property
    public List<Wallet> Wallets { get; private set; } = new();

    // Auditing
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    private User() { } // EF Core constructor

    public User(Guid id, string email, string fullName, string passwordHash, string role = "Customer")
    {
        Id = id;
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        Role = role;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName)
    {
        FullName = fullName.Trim();
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        LastModifiedAtUtc = DateTime.UtcNow;
    }
}
