using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

public record DepositSummary(decimal Paid, decimal Refunded, decimal Applied)
{
    // What the boarding house still holds for the tenant.
    public decimal Held => Paid - Refunded - Applied;
}

public static class DepositService
{
    // Marks a payment that was made from a tenant's deposit.
    public const string PaymentSource = "Deposit";

    // Totals across all of the tenant's stays. "Applied" comes from the payments made from the deposit,
    // so voiding such a payment gives the money back to the deposit automatically.
    public static DepositSummary Summary(AppDbContext db, int tenantId)
    {
        var stays = db.Occupancies.Where(o => o.TenantId == tenantId).ToList();

        var applied = db.Payments
            .Where(p => p.TenantId == tenantId && !p.IsVoided && p.Source == PaymentSource)
            .ToList()
            .Sum(p => p.Amount);

        return new DepositSummary(stays.Sum(o => o.Deposit), stays.Sum(o => o.DepositRefunded), applied);
    }

    // Pays the tenant's oldest unpaid charges from their deposit.
    // Returns an error message, or null when the payment was saved.
    public static string? Apply(AppDbContext db, int tenantId, decimal amount, DateOnly date, string? remarks)
    {
        using var transaction = db.Database.BeginTransaction();

        var summary = Summary(db, tenantId);
        if (amount > summary.Held)
            return $"The amount is more than the credit still held ({Fmt.Peso(summary.Held)}).";

        var open = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.Occupancy.TenantId == tenantId)
            .ToList()
            .Select(c => (c.Id, Balance: c.Amount - c.Allocations.Sum(a => a.AmountApplied), c.DueDate))
            .Where(c => c.Balance > 0)
            .OrderBy(c => c.DueDate).ThenBy(c => c.Id)
            .ToList();

        var unpaid = open.Sum(c => c.Balance);
        if (amount > unpaid)
            return $"The amount is more than the tenant's unpaid balance ({Fmt.Peso(unpaid)}).";

        var allocations = PaymentRules.AllocateOldestFirst(amount, open.Select(c => (c.Id, c.Balance)))
            .Select(a => new PaymentAllocation { ChargeId = a.ChargeId, AmountApplied = a.Applied })
            .ToList();

        if (allocations.Sum(a => a.AmountApplied) != amount)
            return "The unpaid charges changed while this was being saved. Nothing was saved.";

        db.Payments.Add(new Payment
        {
            TenantId = tenantId,
            PaymentDate = date,
            Amount = amount,
            Method = "Credit",
            Source = PaymentSource,
            Remarks = remarks,
            Allocations = allocations
        });
        db.SaveChanges();
        transaction.Commit();   // the payment and its allocations are saved together or not at all
        return null;
    }

    // Gives back part of the credit still held. Only what is left can be refunded.
    // Returns an error message, or null when the refund was saved.
    // The credit is kept per stay, so the refund is taken from the newest stay first.
    public static string? Refund(AppDbContext db, int tenantId, decimal amount, DateOnly date, string? note)
    {
        if (amount <= 0)
            return "Enter the amount to refund as a number greater than zero.";

        using var transaction = db.Database.BeginTransaction();

        var summary = Summary(db, tenantId);
        if (amount > summary.Held)
            return $"The amount is more than the credit still held ({Fmt.Peso(summary.Held)}).";

        var stays = db.Occupancies
            .Where(o => o.TenantId == tenantId)
            .ToList()
            .Where(o => o.Deposit - o.DepositRefunded > 0)
            .OrderByDescending(o => o.MoveInDate).ThenByDescending(o => o.Id)
            .ToList();

        var left = amount;
        foreach (var stay in stays)
        {
            if (left <= 0) break;

            var take = Math.Min(left, stay.Deposit - stay.DepositRefunded);
            stay.DepositRefunded += take;
            stay.DepositRefundedOn = date;
            stay.DepositRefundNote = note;
            left -= take;
        }

        if (left > 0)
            return "The credit changed while this was being saved. Nothing was saved.";

        db.SaveChanges();
        transaction.Commit();   // all the stays are updated together or not at all
        return null;
    }

    // Corrects the credit still held, by changing the deposit recorded on the tenant's stays.
    // More credit is added to the newest stay. Less credit is taken from the newest stays first,
    // and never below what was already refunded. Every change is written to the change history.
    // Returns an error message, or null when the credit was saved.
    public static string? SetCredit(AppDbContext db, int tenantId, decimal newHeld, string reason)
    {
        newHeld = Math.Round(newHeld, 2);
        if (newHeld < 0)
            return "The credit can't be less than zero.";
        if (string.IsNullOrWhiteSpace(reason))
            return "Please enter a reason.";

        using var transaction = db.Database.BeginTransaction();

        var summary = Summary(db, tenantId);
        var change = newHeld - summary.Held;
        if (change == 0)
            return "The credit is already that amount, so nothing was changed.";

        var stays = db.Occupancies
            .Where(o => o.TenantId == tenantId)
            .ToList()
            .OrderByDescending(o => o.MoveInDate).ThenByDescending(o => o.Id)
            .ToList();
        if (stays.Count == 0)
            return "Credit is kept on a stay, and this tenant has no stay yet.";

        void SetDeposit(Occupancy stay, decimal newDeposit)
        {
            AuditService.Record(db, "Stay", stay.Id, tenantId, "Deposit",
                StayEditService.MoneyText(stay.Deposit), StayEditService.MoneyText(newDeposit),
                $"Credit edited: {reason.Trim()}");
            stay.Deposit = newDeposit;
        }

        if (change > 0)
        {
            SetDeposit(stays[0], stays[0].Deposit + change);
        }
        else
        {
            var left = -change;
            foreach (var stay in stays)
            {
                if (left <= 0) break;

                var room = stay.Deposit - stay.DepositRefunded;
                if (room <= 0) continue;

                var take = Math.Min(left, room);
                SetDeposit(stay, stay.Deposit - take);
                left -= take;
            }

            if (left > 0)
                return "The credit changed while this was being saved. Nothing was saved.";
        }

        db.SaveChanges();
        transaction.Commit();   // the new credit and its history entries are saved together or not at all
        return null;
    }

    // Puts refunded money back into the credit (for a refund that was recorded by mistake).
    // Refunds are taken back from the oldest refunded stay first, the reverse of how a refund is taken.
    // Returns an error message, or null when the refund was undone.
    public static string? UndoRefund(AppDbContext db, int tenantId, decimal amount, string reason)
    {
        amount = Math.Round(amount, 2);
        if (amount <= 0)
            return "Enter the amount to put back as a number greater than zero.";
        if (string.IsNullOrWhiteSpace(reason))
            return "Please enter a reason.";

        using var transaction = db.Database.BeginTransaction();

        var stays = db.Occupancies
            .Where(o => o.TenantId == tenantId)
            .ToList()
            .Where(o => o.DepositRefunded > 0)
            .OrderBy(o => o.MoveInDate).ThenBy(o => o.Id)
            .ToList();

        var refunded = stays.Sum(o => o.DepositRefunded);
        if (amount > refunded)
            return $"The amount is more than what was refunded ({Fmt.Peso(refunded)}).";

        var left = amount;
        foreach (var stay in stays)
        {
            if (left <= 0) break;

            var take = Math.Min(left, stay.DepositRefunded);
            var after = stay.DepositRefunded - take;
            AuditService.Record(db, "Stay", stay.Id, tenantId, "DepositRefunded",
                StayEditService.MoneyText(stay.DepositRefunded), StayEditService.MoneyText(after),
                $"Refund undone: {reason.Trim()}");

            stay.DepositRefunded = after;
            if (after == 0)
            {
                stay.DepositRefundedOn = null;
                stay.DepositRefundNote = null;
            }
            left -= take;
        }

        db.SaveChanges();
        transaction.Commit();   // the change and its history entries are saved together or not at all
        return null;
    }

}