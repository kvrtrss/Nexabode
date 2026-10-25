namespace Nexabode.Services;

public record Allocation(int ChargeId, decimal Applied);

// The result of a payment: where it goes, how much of it counts, and what is left over to give back.
public record Settlement(List<Allocation> Allocations, decimal Applied, decimal Change);

public static class PaymentRules
{
    // Applies a payment to charges in the order given (oldest due date first).
    // Each charge receives at most its own balance. Charges with nothing left to pay are skipped.
    public static List<Allocation> AllocateOldestFirst(decimal amount,
       IEnumerable<(int ChargeId, decimal Balance)> openCharges)
    {
        var result = new List<Allocation>();
        var remaining = amount;

        foreach (var (chargeId, balance) in openCharges)
        {
            if (remaining <= 0) break;
            if (balance <= 0) continue;

            var apply = Math.Min(remaining, balance);
            result.Add(new Allocation(chargeId, apply));
            remaining -= apply;
        }

        return result;
    }
    // Applies what was received to the open charges, oldest first. Whatever is more than they need
    // is not kept: it comes back as change to return to the tenant.
    public static Settlement Settle(decimal received, IEnumerable<(int ChargeId, decimal Balance)> openCharges)
    {
        var allocations = AllocateOldestFirst(received, openCharges);
        var applied = allocations.Sum(a => a.Applied);
        return new Settlement(allocations, applied, received - applied);
    }
}