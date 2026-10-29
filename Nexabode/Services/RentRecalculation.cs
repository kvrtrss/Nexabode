using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

// One month's rent charge and what should happen to it.
public sealed class RentPlanItem
{
    public Charge Charge { get; init; } = null!;
    public DateOnly Month { get; init; }
    public RentAction Action { get; init; }
    public decimal CurrentTotal { get; init; }
    public decimal PaidTotal { get; init; }
    public decimal NewAmount { get; init; }
    public decimal Difference { get; init; }
    public string? NewDescription { get; init; }
    public string Text { get; init; } = string.Empty;
}

public static class RentRecalculation
{
    private static string MonthLabel(DateOnly m) => m.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private static bool TryMonth(string key, out DateOnly start) =>
        DateOnly.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out start);

    // Rent for one stay in one month under the property's policy. Null when the stay has no days that month.
    // Mirrors the Rent Billing page: a tenant with another stay in the same month is always billed by days.
    public static RentCalculation? ExpectedRent(decimal agreedRent, DateOnly moveIn, DateOnly? moveOut,
                                                IEnumerable<StayRange> tenantsOtherStays, DateOnly monthStart,
                                                bool fullRentForPartialMonths)
    {
        if (StayRules.DaysIn(new StayRange(0, moveIn, moveOut), monthStart) == 0) return null;

        var others = tenantsOtherStays.ToList();
        var alsoStaysElsewhere = others.Any(o => StayRules.DaysIn(o, monthStart) > 0);
        var byDays = !fullRentForPartialMonths || alsoStaysElsewhere;
        var handover = moveOut is DateOnly outDay && others.Any(o => o.MoveIn == outDay);

        return BillingRules.CalculateRent(agreedRent, moveIn, moveOut, monthStart, byDays, handover);
    }

    // Works out what each existing rent charge of this stay needs. Reads only; changes nothing.
    // 'stay' must still hold its current values. 'edit' holds the proposed ones.
    // The rent amount used is the stay's current agreed rent: a rent change applies from now on.
    public static List<RentPlanItem> Plan(AppDbContext db, Occupancy stay, StayEdit edit,
                                          bool fullRentForPartialMonths)
    {
        var plan = new List<RentPlanItem>();

        var oldRange = new StayRange(stay.Id, stay.MoveInDate, stay.MoveOutDate);
        var newRange = new StayRange(stay.Id, edit.MoveIn, edit.MoveOut);

        var rentCharges = db.Charges.IgnoreQueryFilters()
            .Include(c => c.Allocations)
            .Where(c => c.OccupancyId == stay.Id && c.ChargeType == ChargeType.Rent && !c.IsVoided)
            .ToList();
        if (rentCharges.Count == 0) return plan;

        var ids = rentCharges.Select(c => c.Id).ToList();
        var adjustments = db.Charges.IgnoreQueryFilters()
            .Include(c => c.Allocations)
            .Where(c => c.AdjustsChargeId != null && ids.Contains(c.AdjustsChargeId.Value) && !c.IsVoided)
            .ToList();

        var otherStays = db.Occupancies.AsNoTracking()
            .Where(o => o.TenantId == stay.TenantId && o.Id != stay.Id)
            .ToList()
            .Select(o => new StayRange(o.Id, o.MoveInDate, o.MoveOutDate))
            .ToList();

        foreach (var charge in rentCharges.OrderBy(c => c.BillingMonth))
        {
            if (!TryMonth(charge.BillingMonth, out var month)) continue;

            var oldDays = StayRules.DaysIn(oldRange, month);
            var newDays = StayRules.DaysIn(newRange, month);
            if (oldDays == newDays) continue;   // this change does not touch that month

            var linked = adjustments.Where(a => a.AdjustsChargeId == charge.Id).ToList();
            var currentTotal = charge.Amount + linked.Sum(a => a.Amount);
            var paidTotal = charge.Allocations.Sum(a => a.AmountApplied)
                          + linked.Sum(a => a.Allocations.Sum(x => x.AmountApplied));

            var expected = ExpectedRent(stay.AgreedRent, edit.MoveIn, edit.MoveOut, otherStays, month,
                                        fullRentForPartialMonths);
            var decision = RentAdjustmentRules.Decide(currentTotal, paidTotal, linked.Count > 0, expected?.Amount);
            if (decision.Action == RentAction.None) continue;

            var label = MonthLabel(month);
            var monthDays = DateTime.DaysInMonth(month.Year, month.Month);
            var days = $"days {oldDays} -> {newDays} of {monthDays}";

            var text = decision.Action switch
            {
                RentAction.UpdateInPlace =>
                    $"{label}: charge changes from {Fmt.Peso(currentTotal)} to {Fmt.Peso(decision.NewAmount)} " +
                    $"({expected?.Description}; {days})." +
                    (paidTotal > 0 ? $" The {Fmt.Peso(paidTotal)} already paid stays applied." : ""),

                RentAction.Void =>
                    $"{label}: the {Fmt.Peso(currentTotal)} charge is cancelled (voided) because the stay " +
                    "would have no days in this month.",

                RentAction.AddAdjustment =>
                    $"{label}: {Fmt.Peso(decision.Difference)} more rent is due ({days}). The charge of " +
                    $"{Fmt.Peso(currentTotal)} stays as billed" +
                    (paidTotal > 0 ? $" ({Fmt.Peso(paidTotal)} paid)" : "") +
                    $", and an adjustment charge of {Fmt.Peso(decision.Difference)} is added.",

                RentAction.FlagOverpaid =>
                    $"NEEDS YOUR DECISION - {label}: {Fmt.Peso(paidTotal)} has been paid but only " +
                    $"{Fmt.Peso(decision.NewAmount)} would be due, {Fmt.Peso(paidTotal - decision.NewAmount)} too much. " +
                    "The charge is left as it is. To correct it, return the extra to the tenant, void the payment " +
                    "on the Payments page (enter the amount you gave back), then edit the charge on Billing Charges.",

                _ =>
                    $"NEEDS YOUR DECISION - {label}: only {Fmt.Peso(decision.NewAmount)} would be due instead of " +
                    $"{Fmt.Peso(currentTotal)}, but this charge already has an earlier adjustment. " +
                    "Review it on Billing Charges."
            };

            plan.Add(new RentPlanItem
            {
                Charge = charge,
                Month = month,
                Action = decision.Action,
                CurrentTotal = currentTotal,
                PaidTotal = paidTotal,
                NewAmount = decision.NewAmount,
                Difference = decision.Difference,
                NewDescription = expected?.Description,
                Text = text
            });
        }

        return plan;
    }

    // Makes the planned changes on the tracked charges and records each in the audit trail.
    // The caller saves, together with the stay change, so everything succeeds or fails as one.
    // Returns plain-language lines describing what was done.
    public static List<string> Apply(AppDbContext db, IEnumerable<RentPlanItem> plan, Occupancy stay, string reason)
    {
        var done = new List<string>();
        var today = StayQueries.Today;

        foreach (var item in plan)
        {
            var charge = item.Charge;
            var label = MonthLabel(item.Month);
            var key = charge.BillingMonth;

            switch (item.Action)
            {
                case RentAction.UpdateInPlace:
                    AuditService.Record(db, "Charge", charge.Id, stay.TenantId, $"Rent {key}: amount",
                        StayEditService.MoneyText(charge.Amount), StayEditService.MoneyText(item.NewAmount), reason);
                    if (item.NewDescription != null && item.NewDescription != charge.Description)
                    {
                        AuditService.Record(db, "Charge", charge.Id, stay.TenantId, $"Rent {key}: description",
                            charge.Description, item.NewDescription, reason);
                        charge.Description = item.NewDescription;
                    }
                    charge.Amount = item.NewAmount;
                    done.Add($"{label}: rent changed to {Fmt.Peso(item.NewAmount)}.");
                    break;

                case RentAction.Void:
                    AuditService.Record(db, "Charge", charge.Id, stay.TenantId, $"Rent {key}: voided",
                        StayEditService.MoneyText(charge.Amount), null, reason);
                    charge.IsVoided = true;
                    charge.VoidReason = "Stay dates changed: no billable days left in this month";
                    charge.VoidedOn = today;
                    done.Add($"{label}: the {Fmt.Peso(item.CurrentTotal)} charge was voided.");
                    break;

                case RentAction.AddAdjustment:
                    db.Charges.Add(new Charge
                    {
                        OccupancyId = stay.Id,
                        BillingMonth = charge.BillingMonth,
                        ChargeType = ChargeType.Other,
                        Description = $"Rent adjustment - {label} (stay dates changed)",
                        Amount = item.Difference,
                        DueDate = charge.DueDate > today ? charge.DueDate : today,
                        AdjustsChargeId = charge.Id
                    });
                    AuditService.Record(db, "Charge", charge.Id, stay.TenantId, $"Rent {key}: adjustment added",
                        null, "+" + StayEditService.MoneyText(item.Difference), reason);
                    done.Add($"{label}: an adjustment charge of {Fmt.Peso(item.Difference)} was added.");
                    break;

                default:   // FlagOverpaid, NeedsReview: nothing is changed, but it is written down
                    AuditService.Record(db, "Charge", charge.Id, stay.TenantId, $"Rent {key}: adjustment needed",
                        StayEditService.MoneyText(item.CurrentTotal), StayEditService.MoneyText(item.NewAmount),
                        reason + " (not applied: needs the administrator's decision)");
                    done.Add(item.Text);
                    break;
            }
        }

        return done;
    }
}