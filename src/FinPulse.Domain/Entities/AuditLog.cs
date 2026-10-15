using FinPulse.Domain.Common;
using FinPulse.Domain.Enums;

namespace FinPulse.Domain.Entities;

public class AuditLog : Entity<Guid>
{
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public AuditAction Action { get; private set; }
    public string? PerformedBy { get; private set; }
    public string ChangesJson { get; private set; } = string.Empty;
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    private AuditLog() { } // EF Core constructor

    public AuditLog(
        Guid id,
        string entityName,
        string entityId,
        AuditAction action,
        string? performedBy,
        string changesJson)
    {
        Id = id;
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        PerformedBy = performedBy ?? "System";
        ChangesJson = changesJson;
        TimestampUtc = DateTime.UtcNow;
    }
}
