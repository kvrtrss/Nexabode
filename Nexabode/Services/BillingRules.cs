using Nexabode.Models;

namespace Nexabode.Services;

public record RentCalculation(decimal Amount, string Description, bool IsPartial);

// Billing rules kept free of screens and database code, so they can be tested on their own.
public static class BillingRules
{
    public static List<Occupancy> OneStayPerTenant(IEnumerable<Occupancy> stays) =>
    stays
        .GroupBy(s => s.TenantId)
        .Select(g => g.OrderByDescending(s => s.MoveInDate).ThenByDescending(s => s.Id).First())
        .OrderBy(s => s.Id)
        .ToList();

    // Splits a bill into equal shares that always add up to the exact total.
    // Leftover centavos go to the first share.
    public static decimal[] SplitEvenly(decimal total, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

        var share = Math.Round(total / count, 2, MidpointRounding.ToZero);
        var leftover = total - share * count;

        var shares = Enumerable.Repeat(share, count).ToArray();
        shares[0] += leftover;
        return shares;
    }

    // Rent for one stay in one month. Returns null when the stay has no billable days that month.
    // byDays: bill by the days stayed instead of the full agreed rent.
    // movedStraightIntoAnotherBed: the tenant's next stay starts on this stay's move-out day,
    // so that day belongs to the new stay and is not counted twice.
    public static RentCalculation? CalculateRent(decimal agreedRent, DateOnly moveIn, DateOnly? moveOut,
                                                 DateOnly monthStart, bool byDays,
                                                 bool movedStraightIntoAnotherBed)
    {
        if (!byDays) return new RentCalculation(agreedRent, "Monthly rent", false);

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var daysInMonth = monthEnd.Day;

        var from = moveIn > monthStart ? moveIn : monthStart;
        var to = moveOut is DateOnly mo && mo < monthEnd ? mo : monthEnd;

        // On a handover day the new stay takes over, so this stay ends one day earlier.
        if (movedStraightIntoAnotherBed && moveOut is DateOnly handoverDay && handoverDay <= monthEnd)
            to = to.AddDays(-1);

        var days = to.DayNumber - from.DayNumber + 1;
        if (days <= 0) return null;
        if (days >= daysInMonth) return new RentCalculation(agreedRent, "Monthly rent", false);

        return new RentCalculation(
            Math.Round(agreedRent * days / daysInMonth, 2),
            $"Rent ({days} of {daysInMonth} days)",
            true);
    }

    // refunded: money that was paid on the charge and given back to the tenant.
    // A cancelled (voided) charge that was refunded shows as "Refunded". A charge that is still due always
    // shows how much is owed (Unpaid, Partial, Overdue), so a refund can never hide money that is still owed.
    public static string ChargeStatus(decimal amount, decimal paid, DateOnly dueDate, DateOnly today,
                                      bool isVoided = false, decimal refunded = 0)
    {
        if (isVoided) return refunded > 0 ? "Refunded" : "Voided";
        if (amount - paid <= 0) return "Paid";
        if (dueDate < today) return "Overdue";
        return paid > 0 ? "Partial" : "Unpaid";
    }
}