namespace Nexabode.Services;

public record Allocation(int ChargeId, decimal Applied);

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
}