namespace Nexabode.Services;

public enum RentAction { None, UpdateInPlace, Void, AddAdjustment, FlagOverpaid, NeedsReview }

public sealed record RentDecision(RentAction Action, decimal NewAmount, decimal Difference);

// What to do with one month's rent charge when the correct amount has changed.
// Kept free of screens and database code so it can be tested on its own.
public static class RentAdjustmentRules
{
    // currentTotal: the charge plus any adjustment charges already added to it.
    // paidTotal: everything paid against them. expectedAmount: what is now due (null = nothing due).
    public static RentDecision Decide(decimal currentTotal, decimal paidTotal, bool hasAdjustments,
                                      decimal? expectedAmount)
    {
        var target = expectedAmount ?? 0m;
        var diff = target - currentTotal;

        if (expectedAmount != null && diff == 0) return new(RentAction.None, target, 0);
        if (expectedAmount == null && currentTotal == 0) return new(RentAction.None, 0, 0);

        // Nothing paid and no earlier adjustments: the charge can simply be corrected.
        if (paidTotal <= 0 && !hasAdjustments)
            return expectedAmount == null
                ? new(RentAction.Void, 0, diff)
                : new(RentAction.UpdateInPlace, target, diff);

        // The tenant owes more: add a separate adjustment, never touch what was already billed.
        if (target > currentTotal) return new(RentAction.AddAdjustment, target, diff);

        // The tenant owes less, and an earlier adjustment is involved: a person should look at it.
        if (hasAdjustments) return new(RentAction.NeedsReview, target, diff);

        // Less is owed but the payments still fit under the new amount: correct the charge.
        if (paidTotal <= target) return new(RentAction.UpdateInPlace, target, diff);

        // The tenant has paid more than is now owed. Never change a paid charge on our own.
        return new(RentAction.FlagOverpaid, target, diff);
    }
}