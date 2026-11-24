using System.Globalization;
using Nexabode.Models;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;

namespace Nexabode.Services;

public class MonthOption
{
    public DateOnly Start { get; set; }
    public string Display => Start.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
}

public static class BillingHelpers
{
    // "2026-11" as people say it: "November 2026". Anything that is not a month key is shown as it is.
    public static string MonthLabel(string monthKey) =>
        DateOnly.TryParseExact(monthKey + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
            ? month.ToString("MMMM yyyy", CultureInfo.CurrentCulture)
            : monthKey;

    public static string MonthKey(DateOnly monthStart) =>
        monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // Oldest first, as a calendar reads. Always every month of the current year (January to December) and the 6 months
    // before today, back to the month of the earliest stay. When a stay is planned to end in a later year (a contract
    // that runs over several years), the list goes on to December of that year, so 2026 to 2027 and beyond just works.
    public static List<MonthOption> BuildMonths()
    {
        var thisMonth = new DateOnly(Clock.Today.Year, Clock.Today.Month, 1);
        var earliest = new DateOnly(thisMonth.Year, 1, 1);
        if (thisMonth.AddMonths(-6) < earliest) earliest = thisMonth.AddMonths(-6);
        var latest = new DateOnly(thisMonth.Year, 12, 1);

        try
        {
            using var db = AppDb.Create();
            var firstMoveIn = db.Occupancies
                .OrderBy(o => o.MoveInDate)
                .Select(o => (DateOnly?)o.MoveInDate)
                .FirstOrDefault();

            if (firstMoveIn is DateOnly first)
            {
                var firstMonth = new DateOnly(first.Year, first.Month, 1);
                var limit = thisMonth.AddMonths(-120);   // never more than 10 years back
                if (firstMonth < earliest) earliest = firstMonth < limit ? limit : firstMonth;
            }

            // The furthest planned move-out of a stay that still counts: a contract can run into later years.
            var lastEnd = db.Occupancies
                .Where(o => !o.IsCancelled && o.MoveOutDate != null)
                .OrderByDescending(o => o.MoveOutDate)
                .Select(o => o.MoveOutDate)
                .FirstOrDefault();

            if (lastEnd is DateOnly end && end.Year > latest.Year)
            {
                var limit = thisMonth.AddMonths(60);     // never more than 5 years ahead
                var endYear = new DateOnly(end.Year, 12, 1);
                latest = endYear > limit ? limit : endYear;
            }
        }
        catch
        {
            // the plain year is enough if the stays can't be read
        }

        var months = new List<MonthOption>();
        for (var m = earliest; m <= latest; m = m.AddMonths(1))
            months.Add(new MonthOption { Start = m });
        return months;
    }

    // The usual window plus every month that has billing records (used when archived records are shown).
    public static List<MonthOption> BuildMonthsWithData()
    {
        using var db = AppDb.Create();

        var keys = db.Charges.IgnoreQueryFilters().Select(c => c.BillingMonth)
            .Union(db.UtilityBills.Select(b => b.BillingMonth))
            .ToList();

        var starts = BuildMonths().Select(m => m.Start).ToHashSet();
        foreach (var key in keys)
            if (DateOnly.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var start))
                starts.Add(start);

        return starts.OrderBy(s => s).Select(s => new MonthOption { Start = s }).ToList();
    }

    public static DateOnly CurrentMonthStart() =>
        new(Clock.Today.Year, Clock.Today.Month, 1);

    // What a charge is for: "Rent", or the utility's name (Electricity, Water, ...).
    // The charge must be loaded together with its UtilityBill.
    public static string ItemOf(Charge charge)
    {
        switch (charge.ChargeType)
        {
            case ChargeType.Rent:
                return "Rent";
            case ChargeType.Utility:
                var name = charge.UtilityBill?.UtilityType?.Trim();
                return string.IsNullOrEmpty(name)
                    ? "Utility"
                    : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
            default:
                return "Other";
        }
    }
}