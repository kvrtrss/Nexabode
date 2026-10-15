using System.Globalization;
using Nexabode.Models;

namespace Nexabode.Services;

public class MonthOption
{
    public DateOnly Start { get; set; }
    public string Display => Start.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
}

public static class BillingHelpers
{
    public static string MonthKey(DateOnly monthStart) =>
        monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // Newest first: next month, this month, then 6 months back.
    public static List<MonthOption> BuildMonths()
    {
        var thisMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        return Enumerable.Range(-6, 8)
            .Select(i => new MonthOption { Start = thisMonth.AddMonths(i) })
            .Reverse()
            .ToList();
    }

    public static DateOnly CurrentMonthStart() =>
        new(DateTime.Today.Year, DateTime.Today.Month, 1);

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