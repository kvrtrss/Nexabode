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
            return $"The amount is more than the deposit still held ({Fmt.Peso(summary.Held)}).";

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
            Method = "Deposit",
            Source = PaymentSource,
            Remarks = remarks,
            Allocations = allocations
        });
        db.SaveChanges();
        transaction.Commit();   // the payment and its allocations are saved together or not at all
        return null;
    }
}