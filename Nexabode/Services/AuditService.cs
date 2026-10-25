using Nexabode.Data;
using Nexabode.Models;
using static Nexabode.Models.PaymentAllocation;

namespace Nexabode.Services;

public static class AuditService
{
    // Adds the entry to the same context as the change itself, so both are saved together
    // by the caller's SaveChanges (the correction and its record succeed or fail as one).
    public static void Record(AppDbContext db, string entityType, int entityId, int? tenantId,
                              string field, string? oldValue, string? newValue,
                              string reason, bool conflictException = false)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            ChangedAt = DateTime.Now,
            Administrator = Session.Administrator,
            EntityType = entityType,
            EntityId = entityId,
            TenantId = tenantId,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            IsConflictException = conflictException
        });
    }
}