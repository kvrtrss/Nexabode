using System.Windows;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

// Editing and removing a single stay, shared by the Tenants page and the clash tools on Rooms & Beds, so both
// follow exactly the same safety rules. Each returns true when something was saved. 'afterSave' runs as soon
// as the change is saved and before any message is shown, so the screen can refresh behind the message.
public static class StayActions
{
    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    // ---------- Edit this stay ----------

    public static bool Edit(Window? owner, int stayId, Action? afterSave = null)
    {
        using var db = new AppDbContext();
        var stay = db.Occupancies.Find(stayId);
        if (stay == null) return false;

        var tenantName = db.Tenants.Find(stay.TenantId)?.FullName ?? "";
        var chargeCount = db.Charges.IgnoreQueryFilters().Count(c => c.OccupancyId == stay.Id);

        DepositSummary depositSummary;
        using (var sumDb = new AppDbContext())
            depositSummary = DepositService.Summary(sumDb, stay.TenantId);
        var minDeposit = Math.Max(stay.DepositRefunded, stay.Deposit - depositSummary.Held);

        var beds = db.Beds
            .Include(b => b.Room)
            .ToList()
            .Where(b => b.Id == stay.BedId || (b.IsActive && b.Room.IsActive && !b.IsMaintenance))
            .OrderBy(b => b.Room.RoomName).ThenBy(b => b.BedLabel)
            .Select(b => new BedChoice { Id = b.Id, Display = $"{b.Room.RoomName} - {b.BedLabel}", Rate = b.MonthlyRate })
            .ToList();

        var current = new StayEdit
        {
            BedId = stay.BedId,
            MoveIn = stay.MoveInDate,
            MoveOut = stay.MoveOutDate,
            Rent = stay.AgreedRent,
            Deposit = stay.Deposit
        };

        var note = chargeCount > 0
            ? $"This stay has {chargeCount} billing record(s). Changing its dates does not change them yet. " +
              "Review them in Rent Billing and Billing Charges afterwards."
            : null;

        var result = StayEditDialog.Ask(owner, $"Edit stay: {tenantName}", note, beds, current,
            hardCheck: r => r.Deposit < minDeposit
                ? $"The deposit cannot be less than {Fmt.Peso(minDeposit)}, the part already refunded or used for unpaid charges."
                : null,
            conflictText: r =>
            {
                using var check = new AppDbContext();
                return StayEditService.DescribeConflicts(check, stay, r.BedId, r.MoveIn, r.MoveOut);
            });
        if (result == null) return false;

        var report = StayImpact.Analyze(db, stay, result);
        if (report.HasBillingImpact && !ImpactDialog.Confirm(owner, tenantName, report.ToText(), "Save the stay"))
            return false;

        List<string> billingDone;
        try
        {
            var beforeRange = new StayRange(stay.Id, stay.MoveInDate, stay.MoveOutDate);
            StayEditService.Apply(db, stay, result);
            billingDone = RentRecalculation.Apply(db, report.RentPlan, stay, result.Reason);
            billingDone.AddRange(RentRestore.VoidedMonthNotes(db, stay, beforeRange));
            foreach (var billPlan in report.UtilityPlans)
                billingDone.AddRange(UtilityRecalculation.Apply(db, billPlan, result.Reason));
            db.SaveChanges();   // the stay, its billing corrections and the audit entries are saved together
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The stay could not be saved because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            return false;
        }

        afterSave?.Invoke();

        if (billingDone.Count > 0)
            Tell("The stay was saved. Billing changes and notes:\n\n" + string.Join("\n\n", billingDone));
        return true;
    }

    // ---------- Remove this stay ----------

    // Removes a stay that was entered by mistake: a duplicate, the wrong tenant, the wrong bed. Owners only.
    // A stay that was never billed is deleted. One that has billing records cannot be deleted, because the
    // records point at it and the books must keep adding up, so it is cancelled instead (see Cancel).
    public static bool Remove(Window? owner, int stayId, Action? afterSave = null)
    {
        if (!Session.IsOwner) return false;

        bool billed;
        using (var look = new AppDbContext())
            billed = HasBilling(look, stayId);

        return billed ? Cancel(owner, stayId, afterSave) : Delete(owner, stayId, afterSave);
    }

    // ---------- A stay with no billing: deleted ----------

    private static bool Delete(Window? owner, int stayId, Action? afterSave)
    {
        string tenantName, summary;
        decimal deposit;
        using (var look = new AppDbContext())
        {
            var found = look.Occupancies.AsNoTracking().FirstOrDefault(o => o.Id == stayId);
            if (found == null) { afterSave?.Invoke(); return false; }

            var blocked = StayRemovalRules.BlockedReason(HasBilling(look, stayId), found.DepositRefunded);
            if (blocked != null) { Tell(blocked); return false; }

            tenantName = look.Tenants.Find(found.TenantId)?.FullName ?? "";
            summary = StayEditService.Summary(look, found);
            deposit = found.Deposit;
        }

        var answers = FormDialog.Ask(owner, "Remove this stay", "Continue",
            new[] { ("Reason for removing this stay", "") },
            vals => StayRemovalRules.ReasonError(vals[0]),
            note: StayRemovalRules.FormNote(tenantName, summary, deposit));
        if (answers == null) return false;
        var reason = answers[0].Trim();

        if (!ConfirmPasswordDialog.Ask(owner, Session.Administrator, StayRemovalRules.PasswordNote(tenantName)))
            return false;

        try
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            var stay = db.Occupancies.FirstOrDefault(o => o.Id == stayId);
            if (stay == null) { transaction.Rollback(); afterSave?.Invoke(); return false; }

            // Look again at the last moment, in case something changed while the windows were open.
            var blocked = StayRemovalRules.BlockedReason(HasBilling(db, stayId), stay.DepositRefunded);
            if (blocked != null) { transaction.Rollback(); Tell(blocked); return false; }

            AuditService.Record(db, "Stay", stay.Id, stay.TenantId, "Removed",
                                StayEditService.Summary(db, stay), null, reason);
            db.Occupancies.Remove(stay);
            db.SaveChanges();
            transaction.Commit();
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The stay could not be removed because other records still refer to it. Nothing was changed.");
            return false;
        }

        afterSave?.Invoke();
        return true;
    }

    // ---------- A stay with billing: cancelled ----------

    // Keeps the stay and all its records, but marks it cancelled so that it no longer holds a bed, causes no
    // clash and is not billed. Only possible when nothing active is left on it: every charge voided and no deposit.
    private static bool Cancel(Window? owner, int stayId, Action? afterSave)
    {
        string tenantName, summary;
        using (var look = new AppDbContext())
        {
            var found = look.Occupancies.AsNoTracking().FirstOrDefault(o => o.Id == stayId);
            if (found == null) { afterSave?.Invoke(); return false; }

            var blocked = StayCancelRules.BlockedReason(HasActiveCharges(look, stayId), found.Deposit);
            if (blocked != null) { Tell(blocked); return false; }

            tenantName = look.Tenants.Find(found.TenantId)?.FullName ?? "";
            summary = StayEditService.Summary(look, found);
        }

        var answers = FormDialog.Ask(owner, "Remove this stay", "Continue",
            new[] { ("Reason for removing this stay", "") },
            vals => StayCancelRules.ReasonError(vals[0]),
            note: StayCancelRules.FormNote(tenantName, summary));
        if (answers == null) return false;
        var reason = answers[0].Trim();

        if (!ConfirmPasswordDialog.Ask(owner, Session.Administrator, StayCancelRules.PasswordNote(tenantName)))
            return false;

        try
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            var stay = db.Occupancies.FirstOrDefault(o => o.Id == stayId);
            if (stay == null) { transaction.Rollback(); afterSave?.Invoke(); return false; }

            // Look again at the last moment, in case something changed while the windows were open.
            var blocked = StayCancelRules.BlockedReason(HasActiveCharges(db, stayId), stay.Deposit);
            if (blocked != null) { transaction.Rollback(); Tell(blocked); return false; }

            AuditService.Record(db, "Stay", stay.Id, stay.TenantId, "Cancelled",
                                StayEditService.Summary(db, stay), null, reason);
            stay.IsCancelled = true;
            stay.CancelReason = reason;
            stay.CancelledOn = DateOnly.FromDateTime(DateTime.Today);
            db.SaveChanges();
            transaction.Commit();
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The stay could not be removed because the data changed at the same moment. Nothing was changed.");
            return false;
        }

        afterSave?.Invoke();
        return true;
    }

    // Billing records point at the stay, even voided ones, so any of them stops the stay from being deleted.
    private static bool HasBilling(AppDbContext db, int stayId) =>
        db.Charges.IgnoreQueryFilters().Any(c => c.OccupancyId == stayId);

    // Charges that are not voided (voided ones are filtered out of this query on purpose).
    private static bool HasActiveCharges(AppDbContext db, int stayId) =>
        db.Charges.Any(c => c.OccupancyId == stayId);
}
