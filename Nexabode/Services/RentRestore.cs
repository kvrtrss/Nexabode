using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

// What restoring one voided rent charge would change.
public sealed record RentRestorePlan(
    int ChargeId, int TenantId, string Tenant, DateOnly Month, string BillingMonth,
    decimal OldAmount, string? OldDescription, DateOnly OldDueDate,
    decimal NewAmount, string NewDescription, DateOnly NewDueDate);

// Brings a voided rent charge back. The same charge record is reused, because the database allows only one
// rent charge per stay and month, voided ones included. It is updated to what the stay would be billed now.
public static class RentRestore
{
    private static bool TryMonth(string key, out DateOnly start) =>
        DateOnly.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start);

    private static string MonthLabel(DateOnly m) => m.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    // Reads only; changes nothing. Either a plan or the reason there cannot be one.
    public static (RentRestorePlan? Plan, string? Problem) Plan(AppDbContext db, int chargeId,
                                                                bool fullRentForPartialMonths, DateOnly today)
    {
        var charge = db.Charges.IgnoreQueryFilters()
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .FirstOrDefault(c => c.Id == chargeId && c.ChargeType == ChargeType.Rent);

        if (charge == null || !TryMonth(charge.BillingMonth, out var month))
            return (null, "That rent charge could not be found.");

        var stay = charge.Occupancy;
        var otherStays = db.Occupancies.AsNoTracking()
            .Where(o => o.TenantId == stay.TenantId && o.Id != stay.Id)
            .ToList()
            .Select(o => new StayRange(o.Id, o.MoveInDate, o.MoveOutDate))
            .ToList();

        // Exactly what Rent Billing would have generated for this stay today.
        var expected = RentRecalculation.ExpectedRent(stay.AgreedRent, stay.MoveInDate, stay.MoveOutDate,
                                                      otherStays, month, fullRentForPartialMonths);

        var hasAdjustments = db.Charges.IgnoreQueryFilters()
            .Any(c => c.AdjustsChargeId == charge.Id && !c.IsVoided);

        var blocked = RentRestoreRules.BlockedReason(charge.IsVoided, charge.Allocations.Count > 0,
                                                     hasAdjustments, expected != null);
        if (blocked != null) return (null, blocked);

        // A due date already in the past would make the restored rent overdue the moment it comes back.
        var due = charge.DueDate < today ? today : charge.DueDate;

        return (new RentRestorePlan(charge.Id, stay.TenantId, stay.Tenant.FullName, month, charge.BillingMonth,
                                    charge.Amount, charge.Description, charge.DueDate,
                                    expected!.Amount, expected.Description ?? "Monthly rent", due), null);
    }

    // Makes the change on the tracked charge and writes it to the change history. The caller saves.
    public static void Apply(AppDbContext db, RentRestorePlan plan, string reason)
    {
        var charge = db.Charges.IgnoreQueryFilters().First(c => c.Id == plan.ChargeId);
        var key = plan.BillingMonth;

        AuditService.Record(db, "Charge", charge.Id, plan.TenantId, $"Rent {key}: restored",
                            string.IsNullOrWhiteSpace(charge.VoidReason) ? "Voided" : $"Voided: {charge.VoidReason}",
                            StayEditService.MoneyText(plan.NewAmount), reason);

        if (plan.NewAmount != charge.Amount)
            AuditService.Record(db, "Charge", charge.Id, plan.TenantId, $"Rent {key}: amount",
                                StayEditService.MoneyText(charge.Amount), StayEditService.MoneyText(plan.NewAmount), reason);
        if (plan.NewDescription != charge.Description)
            AuditService.Record(db, "Charge", charge.Id, plan.TenantId, $"Rent {key}: description",
                                charge.Description, plan.NewDescription, reason);
        if (plan.NewDueDate != charge.DueDate)
            AuditService.Record(db, "Charge", charge.Id, plan.TenantId, $"Rent {key}: due date",
                                StayEditService.DateText(charge.DueDate), StayEditService.DateText(plan.NewDueDate), reason);

        charge.Amount = plan.NewAmount;
        charge.Description = plan.NewDescription;
        charge.DueDate = plan.NewDueDate;
        charge.IsVoided = false;
        charge.VoidReason = null;
        charge.VoidedOn = null;
        charge.IsArchived = false;   // it has a balance again
    }

    // After a stay's dates change: the months that now have days again but whose rent is voided. Such a charge
    // is never touched automatically, so the person is told where to restore it. 'stay' holds the new dates.
    public static List<string> VoidedMonthNotes(AppDbContext db, Occupancy stay, StayRange before)
    {
        var notes = new List<string>();
        var now = new StayRange(stay.Id, stay.MoveInDate, stay.MoveOutDate);

        var voided = db.Charges.IgnoreQueryFilters()
            .Where(c => c.OccupancyId == stay.Id && c.ChargeType == ChargeType.Rent && c.IsVoided)
            .ToList();

        foreach (var charge in voided.OrderBy(c => c.BillingMonth))
        {
            if (!TryMonth(charge.BillingMonth, out var month)) continue;

            var daysBefore = StayRules.DaysIn(before, month);
            var daysNow = StayRules.DaysIn(now, month);
            if (daysNow > 0 && daysNow != daysBefore)
                notes.Add(RentRestoreRules.VoidedMonthNote(MonthLabel(month)));
        }

        return notes;
    }
}
