using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

public enum ShareAction { CreateShare, UpdateInPlace, Void, AddAdjustment, FlagOverpaid, NeedsReview }

// A stay as the utility rules see it. The dates may be proposed ones that are not saved yet.
public sealed record ShareStay(int StayId, int TenantId, string TenantName, int RoomId,
                               DateOnly MoveIn, DateOnly? MoveOut);

// Proposed values for one stay, used to preview a change before it is saved.
public sealed record StayOverride(int StayId, int TenantId, int RoomId, DateOnly MoveIn, DateOnly? MoveOut);

public sealed class UtilityPlanItem
{
    public int TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public Charge? Charge { get; init; }          // the tenant's existing share; null for a new share
    public int HostStayId { get; init; }          // the stay that carries a new share
    public ShareAction Action { get; init; }
    public decimal CurrentTotal { get; init; }
    public decimal PaidTotal { get; init; }
    public decimal NewAmount { get; init; }
    public decimal Difference { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class UtilityBillPlan
{
    public UtilityBill Bill { get; init; } = null!;
    public BillPeriod Period { get; init; }
    public string Label { get; init; } = string.Empty;
    public List<UtilityPlanItem> Items { get; } = new();

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Label);
        sb.AppendLine($"Everyone with at least one day between {Fmt.Date(Period.Start)} and {Fmt.Date(Period.End)} pays an equal share.");
        sb.AppendLine();
        foreach (var item in Items) sb.AppendLine("  - " + item.Text);
        sb.AppendLine();
        sb.Append("Paid shares are never altered. Extra amounts become adjustment charges, and items marked " +
                  "NEEDS YOUR DECISION are left exactly as they are.");
        return sb.ToString();
    }
}

public static class UtilityRecalculation
{
    private static string MonthName(string billingMonth) =>
        DateOnly.TryParseExact(billingMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out var m)
            ? m.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : billingMonth;

    public static BillPeriod? PeriodOf(UtilityBill bill) =>
        UtilityRules.EffectivePeriod(bill.BillingMonth, bill.PeriodStart, bill.PeriodEnd);

    public static string BillLabel(UtilityBill bill)
    {
        var scope = bill.RoomId == null ? "whole house" : bill.Room?.RoomName ?? "one room";
        var label = $"{MonthName(bill.BillingMonth)} {bill.UtilityType} bill ({scope}, {Fmt.Peso(bill.TotalAmount)}";
        if (bill.PeriodStart is DateOnly s && bill.PeriodEnd is DateOnly e)
            label += $", {Fmt.Date(s)} to {Fmt.Date(e)}";
        return label + ")";
    }

    // The stays that share a bill: those with a day in its period (and in its room, for a room bill),
    // one per tenant. With 'ov', one stay is treated as if it already had its proposed values.
    public static List<ShareStay> SharingStays(AppDbContext db, UtilityBill bill, BillPeriod period,
                                               StayOverride? ov)
    {
        var ovId = ov?.StayId ?? -1;
        var from = period.Start;
        var to = period.End;

        var raw = db.Occupancies.AsNoTracking()
            .Include(o => o.Tenant)
            .Include(o => o.Bed)
            .Where(o => (o.MoveInDate <= to && (o.MoveOutDate == null || o.MoveOutDate >= from)) || o.Id == ovId)
            .ToList();

        var views = raw
            .Select(o => o.Id == ovId && ov != null
                ? new ShareStay(o.Id, o.TenantId, o.Tenant.FullName, ov.RoomId, ov.MoveIn, ov.MoveOut)
                : new ShareStay(o.Id, o.TenantId, o.Tenant.FullName, o.Bed.RoomId, o.MoveInDate, o.MoveOutDate))
            .Where(v => UtilityRules.Overlaps(new StayRange(v.StayId, v.MoveIn, v.MoveOut), period)
                        && (bill.RoomId == null || bill.RoomId == v.RoomId))
            .ToList();

        // One share per tenant: a tenant with two stays in scope is not charged twice.
        return views
            .GroupBy(v => v.TenantId)
            .Select(g => g.OrderByDescending(v => v.MoveIn).ThenByDescending(v => v.StayId).First())
            .OrderBy(v => v.StayId)
            .ToList();
    }

    private static ShareAction Map(RentAction action) => action switch
    {
        RentAction.UpdateInPlace => ShareAction.UpdateInPlace,
        RentAction.Void => ShareAction.Void,
        RentAction.AddAdjustment => ShareAction.AddAdjustment,
        RentAction.FlagOverpaid => ShareAction.FlagOverpaid,
        _ => ShareAction.NeedsReview
    };

    // Compares what each tenant has been billed with what the equal split gives today.
    // Null if the bill has not been split (or its period can't be read). Reads only; changes nothing.
    public static UtilityBillPlan? Plan(AppDbContext db, UtilityBill bill, StayOverride? ov)
    {
        if (PeriodOf(bill) is not BillPeriod period) return null;

        var shares = db.Charges.IgnoreQueryFilters()
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Where(c => c.UtilityBillId == bill.Id && !c.IsVoided)
            .ToList();
        if (shares.Count == 0) return null;

        var shareIds = shares.Select(c => c.Id).ToList();
        var adjustments = db.Charges.IgnoreQueryFilters()
            .Include(c => c.Allocations)
            .Where(c => c.AdjustsChargeId != null && shareIds.Contains(c.AdjustsChargeId.Value) && !c.IsVoided)
            .ToList();

        var sharing = SharingStays(db, bill, period, ov);
        var amounts = sharing.Count > 0
            ? BillingRules.SplitEvenly(bill.TotalAmount, sharing.Count)
            : Array.Empty<decimal>();

        var plan = new UtilityBillPlan { Bill = bill, Period = period, Label = BillLabel(bill) };

        var tenantIds = sharing.Select(s => s.TenantId)
            .Concat(shares.Select(c => c.Occupancy.TenantId))
            .Distinct()
            .ToList();

        foreach (var tenantId in tenantIds)
        {
            var index = sharing.FindIndex(s => s.TenantId == tenantId);
            decimal? target = index >= 0 ? amounts[index] : null;

            var mine = shares.Where(c => c.Occupancy.TenantId == tenantId).OrderBy(c => c.Id).ToList();
            var name = index >= 0 ? sharing[index].TenantName : mine[0].Occupancy.Tenant.FullName;

            // Someone with days in the period but no share yet.
            if (mine.Count == 0)
            {
                plan.Items.Add(new UtilityPlanItem
                {
                    TenantId = tenantId,
                    TenantName = name,
                    HostStayId = sharing[index].StayId,
                    Action = ShareAction.CreateShare,
                    NewAmount = target!.Value,
                    Text = $"{name}: has days in this bill's period but no share, so a new share of " +
                           $"{Fmt.Peso(target.Value)} is added."
                });
                continue;
            }

            var linked = adjustments.Where(a => mine.Any(m => m.Id == a.AdjustsChargeId)).ToList();
            var currentTotal = mine.Sum(c => c.Amount) + linked.Sum(a => a.Amount);
            var paidTotal = mine.Sum(c => c.Allocations.Sum(x => x.AmountApplied))
                          + linked.Sum(a => a.Allocations.Sum(x => x.AmountApplied));

            var decision = RentAdjustmentRules.Decide(currentTotal, paidTotal,
                                                       linked.Count > 0 || mine.Count > 1, target);
            if (decision.Action == RentAction.None) continue;

            var text = decision.Action switch
            {
                RentAction.UpdateInPlace =>
                    $"{name}: share changes from {Fmt.Peso(currentTotal)} to {Fmt.Peso(decision.NewAmount)}." +
                    (paidTotal > 0 ? $" The {Fmt.Peso(paidTotal)} already paid stays applied." : ""),

                RentAction.Void =>
                    $"{name}: the {Fmt.Peso(currentTotal)} share is cancelled (voided) because they have no days " +
                    "in this bill's period.",

                RentAction.AddAdjustment =>
                    $"{name}: {Fmt.Peso(decision.Difference)} more is due. The share of {Fmt.Peso(currentTotal)} " +
                    "stays as billed" + (paidTotal > 0 ? $" ({Fmt.Peso(paidTotal)} paid)" : "") +
                    $", and an adjustment charge of {Fmt.Peso(decision.Difference)} is added.",

                RentAction.FlagOverpaid =>
                    $"NEEDS YOUR DECISION - {name}: {Fmt.Peso(paidTotal)} has been paid but only " +
                    $"{Fmt.Peso(decision.NewAmount)} would be due, {Fmt.Peso(paidTotal - decision.NewAmount)} too much. " +
                    "The share is left as it is. To correct it, return the extra to the tenant, void the payment " +
                    "on the Payments page (enter the amount you gave back), then use Recalculate shares on the Utility Bills page.",

                _ =>
                    $"NEEDS YOUR DECISION - {name}: only {Fmt.Peso(decision.NewAmount)} would be due instead of " +
                    $"{Fmt.Peso(currentTotal)}, but this share already has an earlier adjustment. " +
                    "Review it on Billing Charges."
            };

            plan.Items.Add(new UtilityPlanItem
            {
                TenantId = tenantId,
                TenantName = name,
                Charge = mine[0],
                Action = Map(decision.Action),
                CurrentTotal = currentTotal,
                PaidTotal = paidTotal,
                NewAmount = decision.NewAmount,
                Difference = decision.Difference,
                Text = text
            });
        }

        return plan;
    }

    // The split bills whose shares a stay change would affect: those where this tenant joins or leaves.
    public static List<UtilityBillPlan> PlansForStayChange(AppDbContext db, StayOverride ov)
    {
        var splitIds = db.Charges.IgnoreQueryFilters()
            .Where(c => c.UtilityBillId != null && !c.IsVoided)
            .Select(c => c.UtilityBillId!.Value)
            .Distinct()
            .ToList();

        var bills = db.UtilityBills.Include(b => b.Room)
            .Where(b => !b.IsArchived && splitIds.Contains(b.Id))
            .ToList()
            .OrderBy(b => b.BillingMonth).ThenBy(b => b.UtilityType)
            .ToList();

        var plans = new List<UtilityBillPlan>();
        foreach (var bill in bills)
        {
            if (PeriodOf(bill) is not BillPeriod period) continue;

            var before = SharingStays(db, bill, period, null).Any(s => s.TenantId == ov.TenantId);
            var after = SharingStays(db, bill, period, ov).Any(s => s.TenantId == ov.TenantId);
            if (before == after) continue;

            var plan = Plan(db, bill, ov);
            if (plan != null && plan.Items.Count > 0) plans.Add(plan);
        }
        return plans;
    }

    // Makes the planned changes on the tracked charges and records each in the audit trail.
    // The caller saves, so a stay change and its billing changes succeed or fail together.
    public static List<string> Apply(AppDbContext db, UtilityBillPlan plan, string reason)
    {
        var done = new List<string>();
        var bill = plan.Bill;
        var today = StayQueries.Today;
        var month = MonthName(bill.BillingMonth);

        foreach (var item in plan.Items)
        {
            var field = $"{bill.UtilityType} {bill.BillingMonth}";

            switch (item.Action)
            {
                case ShareAction.CreateShare:
                    db.Charges.Add(new Charge
                    {
                        OccupancyId = item.HostStayId,
                        BillingMonth = bill.BillingMonth,
                        ChargeType = ChargeType.Utility,
                        Description = $"{bill.UtilityType} share",
                        Amount = item.NewAmount,
                        DueDate = bill.DueDate ?? plan.Period.End,
                        UtilityBillId = bill.Id
                    });
                    AuditService.Record(db, "UtilityBill", bill.Id, item.TenantId, $"{field}: share added",
                        null, StayEditService.MoneyText(item.NewAmount), reason);
                    done.Add($"{item.TenantName}: new {bill.UtilityType} share of {Fmt.Peso(item.NewAmount)} ({month}).");
                    break;

                case ShareAction.UpdateInPlace:
                    AuditService.Record(db, "UtilityBill", bill.Id, item.TenantId, $"{field}: share amount",
                        StayEditService.MoneyText(item.Charge!.Amount), StayEditService.MoneyText(item.NewAmount), reason);
                    item.Charge.Amount = item.NewAmount;
                    done.Add($"{item.TenantName}: {bill.UtilityType} share ({month}) changed to {Fmt.Peso(item.NewAmount)}.");
                    break;

                case ShareAction.Void:
                    AuditService.Record(db, "UtilityBill", bill.Id, item.TenantId, $"{field}: share voided",
                        StayEditService.MoneyText(item.Charge!.Amount), null, reason);
                    item.Charge.IsVoided = true;
                    item.Charge.VoidReason = "Stay changed: no days in this bill's period";
                    item.Charge.VoidedOn = today;
                    done.Add($"{item.TenantName}: {bill.UtilityType} share ({month}) of {Fmt.Peso(item.CurrentTotal)} was voided.");
                    break;

                case ShareAction.AddAdjustment:
                    db.Charges.Add(new Charge
                    {
                        OccupancyId = item.Charge!.OccupancyId,
                        BillingMonth = bill.BillingMonth,
                        ChargeType = ChargeType.Other,
                        Description = $"{bill.UtilityType} adjustment - {month} (stay dates changed)",
                        Amount = item.Difference,
                        DueDate = item.Charge.DueDate > today ? item.Charge.DueDate : today,
                        AdjustsChargeId = item.Charge.Id
                    });
                    AuditService.Record(db, "UtilityBill", bill.Id, item.TenantId, $"{field}: adjustment added",
                        null, "+" + StayEditService.MoneyText(item.Difference), reason);
                    done.Add($"{item.TenantName}: an adjustment charge of {Fmt.Peso(item.Difference)} was added ({bill.UtilityType}, {month}).");
                    break;

                default:   // FlagOverpaid, NeedsReview: nothing is changed, but it is written down
                    AuditService.Record(db, "UtilityBill", bill.Id, item.TenantId, $"{field}: adjustment needed",
                        StayEditService.MoneyText(item.CurrentTotal), StayEditService.MoneyText(item.NewAmount),
                        reason + " (not applied: needs the administrator's decision)");
                    done.Add(item.Text);
                    break;
            }
        }

        return done;
    }
}