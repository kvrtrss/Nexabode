namespace Nexabode.Models;

public enum ChargeType { Rent, Utility, Other }

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Owner";          // "Owner" can do everything, "Staff" works with records only
    public bool MustChangePassword { get; set; }          // true after an account is added or its password is reset
    public string? RecoveryCodeHash { get; set; }          // BCrypt hash of the account's recovery code (the code itself is never stored)
}

// One row that remembers failed sign-in attempts, so closing and reopening the app can't reset the count.
public class SignInGuard
{
    public int Id { get; set; }
    public int FailedAttempts { get; set; }      // failed attempts in the current round
    public int LockLevel { get; set; }           // how many times sign-in has been locked in a row (longer locks each time)
    public DateTime? LockedUntilUtc { get; set; } // null = not locked
    public string? EmergencyKeyHash { get; set; } // BCrypt hash of the emergency access key (never the key itself)
    public string? ResetPhraseHash { get; set; }  // BCrypt hash of the Owner's reset phrase (never the phrase itself)
}

public class Room
{
    public int Id { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Bed> Beds { get; set; } = new();
}

public class Bed
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public string BedLabel { get; set; } = string.Empty;
    public decimal MonthlyRate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsMaintenance { get; set; }
    public List<Occupancy> Occupancies { get; set; } = new();
}

public class Tenant
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? EmergencyContact { get; set; }
    public string? SchoolOrWork { get; set; }
    public string? Notes { get; set; }
    public string? Gender { get; set; }   // Male, Female or Other; null = not set
    public List<Occupancy> Occupancies { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

public class Occupancy
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int BedId { get; set; }
    public Bed Bed { get; set; } = null!;
    public DateOnly MoveInDate { get; set; }
    public DateOnly? MoveOutDate { get; set; }   // ?null = still staying
    public decimal AgreedRent { get; set; }
    public decimal Deposit { get; set; }
    public decimal DepositRefunded { get; set; }
    public DateOnly? DepositRefundedOn { get; set; }
    public string? DepositRefundNote { get; set; }
    public DateOnly? MovedOutOn { get; set; }   // the day the administrator confirmed the tenant had left (set by Move out)
    public bool IsCancelled { get; set; }       // entered by mistake but already billed: kept for the records, treated as never held
    public string? CancelReason { get; set; }
    public DateOnly? CancelledOn { get; set; }
    public List<Charge> Charges { get; set; } = new();
}

public class Reminder
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateOnly DueDate { get; set; }
    public bool IsDone { get; set; }
}

public class UtilityBill
{
    public int Id { get; set; }
    public string UtilityType { get; set; } = string.Empty;
    public string BillingMonth { get; set; } = string.Empty;  // "eg2026-10"
    public decimal TotalAmount { get; set; }
    public int? RoomId { get; set; }                          // ?null = whole house
    public Room? Room { get; set; }
    public DateOnly? DueDate { get; set; }
    public List<Charge> Charges { get; set; } = new();
    public bool IsArchived { get; set; }
    public DateOnly? PeriodStart { get; set; }   // blank = the whole month in BillingMonth
    public DateOnly? PeriodEnd { get; set; }
}

public class Charge
{
    public int Id { get; set; }
    public int OccupancyId { get; set; }
    public Occupancy Occupancy { get; set; } = null!;
    public string BillingMonth { get; set; } = string.Empty;
    public ChargeType ChargeType { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public int? UtilityBillId { get; set; }
    public UtilityBill? UtilityBill { get; set; }
    public List<PaymentAllocation> Allocations { get; set; } = new();
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }
    public DateOnly? VoidedOn { get; set; }
    public bool IsArchived { get; set; }
    public int? AdjustsChargeId { get; set; }
    public decimal RefundedAmount { get; set; }   // paid money that was given back to the tenant (entered when a payment is voided)
    public DateOnly? RefundedOn { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? ReferenceNo { get; set; }       // typed: the GCash, bank or check reference, if there is one
    public string? ReceiptNo { get; set; }         // assigned automatically when the payment is saved, e.g. RCT-2026-00042
    public string? Remarks { get; set; }
    public bool IsVoided { get; set; }          
    public string? VoidReason { get; set; }
    public DateOnly? VoidedOn { get; set; }
    public List<PaymentAllocation> Allocations { get; set; } = new();
    public decimal? AmountReceived { get; set; }   // what the tenant handed over; blank on older payments (= Amount)
    public string? Source { get; set; }   // "Deposit" for a payment made from a tenant's deposit
    public bool IsArchived { get; set; }
}

public class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public int ChargeId { get; set; }
    public Charge Charge { get; set; } = null!;
    public decimal AmountApplied { get; set; }

    public class AuditLogEntry
    {
        public int Id { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Administrator { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;   // "Stay" now; "Charge" / "UtilityBill" in later phases
        public int EntityId { get; set; }
        public int? TenantId { get; set; }
        public string Field { get; set; } = string.Empty;        // e.g. "MoveInDate"
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool IsConflictException { get; set; }            // saved despite a bed conflict
    }

}