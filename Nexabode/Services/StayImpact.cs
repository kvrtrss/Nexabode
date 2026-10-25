using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

public sealed class StayImpactReport
{
    public string Heading { get; set; } = string.Empty;
    public List<string> BedLines { get; } = new();
    public List<string> RentLines { get; } = new();
    public List<string> UtilityLines { get; } = new();
    public List<string> DepositLines { get; } = new();
    public List<RentPlanItem> RentPlan { get; } = new();
    public List<UtilityBillPlan> UtilityPlans { get; } = new();
    public bool HasPaidRecords { get; set; }

    // Only billing-related effects make the preview appear. Bed changes alone do not.
    public bool HasBillingImpact => RentLines.Count > 0 || UtilityLines.Count > 0 || DepositLines.Count > 0;

    private bool WillChangeBilling =>
        RentPlan.Any(p => p.Action is RentAction.UpdateInPlace or RentAction.Void or RentAction.AddAdjustment) ||
        UtilityPlans.Any(p => p.Items.Any(i => i.Action is ShareAction.CreateShare or ShareAction.UpdateInPlace
                                                or ShareAction.Void or ShareAction.AddAdjustment));

    private bool NeedsDecision =>
        RentPlan.Any(p => p.Action is RentAction.FlagOverpaid or RentAction.NeedsReview) ||
        UtilityPlans.Any(p => p.Items.Any(i => i.Action is ShareAction.FlagOverpaid or ShareAction.NeedsReview));

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Heading);
        AddSection(sb, "BEDS AND OCCUPANCY", BedLines);
        AddSection(sb, "RENT", RentLines);
        AddSection(sb, "UTILITIES", UtilityLines);
        AddSection(sb, "DEPOSIT", DepositLines);

        sb.AppendLine();
        if (HasPaidRecords)
            sb.AppendLine("Paid charges are never altered. Extra amounts are added as separate adjustment charges.");
        if (WillChangeBilling)
            sb.AppendLine("If you apply this change, the rent and utility corrections described above are made " +
                          "automatically and recorded in the change history.");
        if (NeedsDecision)
            sb.AppendLine("Items marked NEEDS YOUR DECISION are left exactly as they are.");
        sb.Append("Months with no rent charge yet, and deposits, are not changed automatically.");
        return sb.ToString();
    }

    private static void AddSection(StringBuilder sb, string title, List<string> lines)
    {
        if (lines.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine(title);
        foreach (var line in lines) sb.AppendLine(line.StartsWith("   ") ? line : "  - " + line);
    }
}

// Works out what a stay change would affect. It only reads data and never saves anything.
public static class StayImpact
{
    private static string D(DateOnly d) => Fmt.Date(d);

    private static string RangeText(StayRange r) =>
        r.MoveOut is DateOnly o ? $"{D(r.MoveIn)} to {D(o)}" : $"{D(r.MoveIn)} onward (no move-out date)";

    private static string SpanText(DateSpan s) =>
        s.End is DateOnly e ? (e == s.Start ? D(s.Start) : $"{D(s.Start)} to {D(e)}") : $"{D(s.Start)} onward";

    private static string MonthLabel(DateOnly m) => m.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private static bool TryMonth(string key, out DateOnly start) =>
        DateOnly.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out start);

    // 'stay' must still hold its current (unchanged) values.
    public static StayImpactReport Analyze(AppDbContext db, Occupancy stay, StayEdit edit)
    {
        var report = new StayImpactReport();
        var today = StayQueries.Today;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        var oldRange = new StayRange(stay.Id, stay.MoveInDate, stay.MoveOutDate);
        var newRange = new StayRange(stay.Id, edit.MoveIn, edit.MoveOut);

        var oldBed = StayEditService.BedName(db, stay.BedId);
        var newBed = StayEditService.BedName(db, edit.BedId);
        report.Heading = $"{oldBed}, {RangeText(oldRange)}\n   changes to\n{newBed}, {RangeText(newRange)}";

        // ----- Beds and occupancy -----
        if (edit.BedId != stay.BedId) report.BedLines.Add($"Bed: {oldBed} -> {newBed}.");
        if (edit.MoveIn != stay.MoveInDate)
            report.BedLines.Add($"Move-in: {D(stay.MoveInDate)} -> {D(edit.MoveIn)}.");
        if (edit.MoveOut != stay.MoveOutDate)
            report.BedLines.Add("Move-out: " +
                (stay.MoveOutDate is DateOnly oldOut ? D(oldOut) : "none (ongoing)") + " -> " +
                (edit.MoveOut is DateOnly newOut ? D(newOut) : "none (ongoing)") + ".");

        var freed = edit.BedId != stay.BedId
            ? new List<DateSpan> { new(oldRange.MoveIn, oldRange.MoveOut) }
            : StayRules.Subtract(oldRange, newRange);
        var taken = edit.BedId != stay.BedId
            ? new List<DateSpan> { new(newRange.MoveIn, newRange.MoveOut) }
            : StayRules.Subtract(newRange, oldRange);
        foreach (var span in freed) report.BedLines.Add($"{oldBed} becomes free for {SpanText(span)}.");
        foreach (var span in taken) report.BedLines.Add($"{newBed} becomes occupied for {SpanText(span)}.");

        // The tenant's other stays matter for the rent policy.
        var otherRanges = db.Occupancies.AsNoTracking()
            .Where(o => o.TenantId == stay.TenantId && o.Id != stay.Id)
            .ToList()
            .Select(o => new StayRange(o.Id, o.MoveInDate, o.MoveOutDate))
            .ToList();

        // ----- Rent: the plan that Apply will carry out -----
        var fullRent = AppSettings.Load().FullRentForPartialMonths;
        foreach (var item in RentRecalculation.Plan(db, stay, edit, fullRent))
        {
            report.RentPlan.Add(item);
            report.RentLines.Add(item.Text);
            if (item.PaidTotal > 0) report.HasPaidRecords = true;
        }

        // Months the stay would newly cover that have no rent charge yet: listed for information only.
        var chargedKeys = db.Charges.IgnoreQueryFilters()
            .Where(c => c.OccupancyId == stay.Id && c.ChargeType == ChargeType.Rent)
            .Select(c => c.BillingMonth)
            .ToList();

        foreach (var month in MonthsToCheck(oldRange, newRange, chargedKeys, thisMonth))
        {
            if (month > thisMonth || chargedKeys.Contains(BillingHelpers.MonthKey(month))) continue;
            if (StayRules.DaysIn(oldRange, month) != 0) continue;

            var newDays = StayRules.DaysIn(newRange, month);
            if (newDays == 0) continue;

            var expected = RentRecalculation.ExpectedRent(stay.AgreedRent, edit.MoveIn, edit.MoveOut,
                                                          otherRanges, month, fullRent);
            report.RentLines.Add(
                $"{MonthLabel(month)}: no rent charge exists, and this month would now be covered " +
                $"({newDays} of {DateTime.DaysInMonth(month.Year, month.Month)} days). " +
                "Generate it on Rent Billing" +
                (expected != null ? $" (about {Fmt.Peso(expected.Amount)})." : "."));
        }

        // ----- Utilities: the plans that Apply will carry out -----
        var roomId = db.Beds.Where(b => b.Id == edit.BedId).Select(b => b.RoomId).First();
        var ov = new StayOverride(stay.Id, stay.TenantId, roomId, edit.MoveIn, edit.MoveOut);

        foreach (var billPlan in UtilityRecalculation.PlansForStayChange(db, ov))
        {
            report.UtilityPlans.Add(billPlan);
            report.UtilityLines.Add(billPlan.Label + ":");
            foreach (var item in billPlan.Items)
            {
                report.UtilityLines.Add("      " + item.Text);
                if (item.PaidTotal > 0) report.HasPaidRecords = true;
            }
        }

        // ----- Deposit -----
        if (edit.Deposit != stay.Deposit)
        {
            var summary = DepositService.Summary(db, stay.TenantId);
            report.DepositLines.Add(
                $"The deposit for this stay changes from {Fmt.Peso(stay.Deposit)} to {Fmt.Peso(edit.Deposit)}. " +
                $"Already refunded on this stay: {Fmt.Peso(stay.DepositRefunded)}. " +
                $"Used for unpaid charges (all stays): {Fmt.Peso(summary.Applied)}.");
        }

        return report;
    }

    // Every calendar month the old or new dates touch, plus any month that already has a rent charge.
    private static List<DateOnly> MonthsToCheck(StayRange a, StayRange b, IEnumerable<string> chargeKeys,
                                                DateOnly thisMonth)
    {
        var horizon = thisMonth.AddMonths(1);   // billing is never generated beyond next month

        var first = a.MoveIn < b.MoveIn ? a.MoveIn : b.MoveIn;
        var aEnd = a.MoveOut ?? horizon;
        var bEnd = b.MoveOut ?? horizon;
        var last = aEnd > bEnd ? aEnd : bEnd;

        foreach (var key in chargeKeys)
        {
            if (!TryMonth(key, out var m)) continue;
            if (m < first) first = m;
            if (m > last) last = m;
        }

        var months = new List<DateOnly>();
        for (var m = new DateOnly(first.Year, first.Month, 1); m <= last && months.Count < 240; m = m.AddMonths(1))
            months.Add(m);
        return months;
    }
}