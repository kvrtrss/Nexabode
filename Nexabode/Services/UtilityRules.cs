using System.Globalization;

namespace Nexabode.Services;

public readonly record struct BillPeriod(DateOnly Start, DateOnly End);

// Utility billing rules kept free of screens and database code, so they can be tested on their own.
public static class UtilityRules
{
    // The days a bill covers: its own period if it has one, otherwise the whole month of its BillingMonth ("yyyy-MM").
    // Null if the period runs backwards or the month can't be read.
    public static BillPeriod? EffectivePeriod(string billingMonth, DateOnly? periodStart, DateOnly? periodEnd)
    {
        if (periodStart is DateOnly s && periodEnd is DateOnly e)
            return e >= s ? new BillPeriod(s, e) : null;

        if (!DateOnly.TryParseExact(billingMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out var first))
            return null;

        return new BillPeriod(first, first.AddMonths(1).AddDays(-1));
    }

    // A stay shares a bill if it has at least one day in the period (both end days count).
    public static bool Overlaps(StayRange stay, BillPeriod period) =>
        StayRules.Overlaps(stay, new StayRange(0, period.Start, period.End));

    public static bool PeriodsOverlap(BillPeriod a, BillPeriod b) =>
        a.Start <= b.End && b.Start <= a.End;
}