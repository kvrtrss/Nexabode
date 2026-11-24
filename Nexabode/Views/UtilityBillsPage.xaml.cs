using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class UtilityBillsPage : Page
{
    public class ScopeOption
    {
        public int? RoomId { get; set; }
        public string Display { get; set; } = "";
    }

    // One tenant's part of a bill, for the details under the row.
    public class ShareLine
    {
        public string Tenant { get; set; } = "";
        public string Place { get; set; } = "";
        public decimal Amount { get; set; }
        public string StatusText { get; set; } = "";   // Paid / Partial / Unpaid, or "Not split" before the split
    }

    public class BillRow
    {
        public int Id { get; set; }
        public string UtilityType { get; set; } = "";
        public string Scope { get; set; } = "";
        public decimal Total { get; set; }
        public DateOnly? Due { get; set; }
        public string DueText => Due?.ToString("MMM d, yyyy") ?? "-";
        public bool IsSplit { get; set; }
        public string PeriodText { get; set; } = "";
        public bool IsArchived { get; set; }
        public bool Settled { get; set; }   // split, and every share is fully paid
        public string StatusText => IsArchived && Settled ? "Archived" : IsSplit ? "Split" : "Draft";

        // Two-line rows: second line of the first column (where and which days), and of the total column (the due date).
        public string LineTwo => $"{Scope}  ·  {PeriodText}  ·  {TenantCountText}";

        // Who the bill is billed to (or would be, before the split).
        public List<ShareLine> Shares { get; set; } = new();
        public string ScopeNote { get; set; } = "";
        public string TenantCountText => Shares.Count == 0 ? "no tenants"
            : IsSplit ? Fmt.Count(Shares.Count, "tenant") : Fmt.Count(Shares.Count, "tenant") + " if split";
        public string BilledTitle => Shares.Count == 0
            ? "No tenants stayed there during this period, so this bill cannot be split yet."
            : IsSplit ? "Billed to " + Fmt.Count(Shares.Count, "tenant")
            : "Not split yet. If you split it now, it is billed to " + Fmt.Count(Shares.Count, "tenant");
        public string DueLine => Due is null ? "" : $"Due {DueText}";
    }

    private bool _ready; 
    private List<BillRow> _bills = new();

    public UtilityBillsPage()
    {
        InitializeComponent();

        // Right-click on a row offers the same actions as the buttons.
        GridMenu.Attach(BillGrid, SplitButton, EditButton, UndoButton, RecalcButton, DeleteButton, ArchiveButton, UnarchiveButton);
        Loaded += (_, _) => Setup();
    }

    // ---------- Helpers ----------


    private Window? Owner => Window.GetWindow(this);

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out date);

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

    private BillRow? Selected =>
    BillGrid.SelectedItems.Count == 1 ? BillGrid.SelectedItem as BillRow : null;

    private List<BillRow> SelectedBills =>
        BillGrid.SelectedItems.OfType<BillRow>().ToList();

    private void Setup()
    {
        var months = BillingHelpers.BuildMonths();
        MonthCombo.ItemsSource = months;
        _ready = true;
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || SelectedMonth is not DateOnly) return;
        LoadBills();
    }

    private void BillGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    // Opens the little menu with the less common actions.
    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = MoreButton;
        menu.IsOpen = true;
    }

    private void ShowArchived_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        var keep = SelectedMonth;
        var months = ShowArchivedCheck.IsChecked == true
            ? BillingHelpers.BuildMonthsWithData()
            : BillingHelpers.BuildMonths();

        MonthCombo.ItemsSource = months;
        MonthCombo.SelectedItem = months.FirstOrDefault(m => m.Start == keep)
                                  ?? months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void Archive_Click(object sender, RoutedEventArgs e) => SetArchived(true);

    private void Unarchive_Click(object sender, RoutedEventArgs e) => SetArchived(false);

    private void SetArchived(bool archived)
    {
        var ids = SelectedBills
            .Where(r => r.IsArchived != archived && (!archived || r.Settled))
            .Select(r => r.Id)
            .ToList();
        ArchiveBills(ids, archived);
    }

    private void ArchiveSettled_Click(object sender, RoutedEventArgs e)
    {
        var ids = _bills.Where(r => !r.IsArchived && r.Settled).Select(r => r.Id).ToList();
        if (ids.Count == 0)
        {
            Say.Info("There are no fully paid bills to archive for this month.");
            return;
        }

        var month = (MonthCombo.SelectedItem as MonthOption)?.Display;
        var answer = ConfirmActionDialog.YesNo(
            $"Archive {Fmt.Count(ids.Count, "fully paid bill")} for {month}?\n\n" +
            "They are only hidden from this list. Totals, reports and statements are not affected. " +
            "The charges for these bills stay in Billing Charges until you archive them there. You can show the bills again with Show archived and Unarchive.",
            "Archive settled bills", confirmText: "Archive");
        if (answer != MessageBoxResult.Yes) return;

        ArchiveBills(ids, true);
    }

    private void ArchiveBills(List<int> ids, bool archived)
    {
        if (ids.Count == 0) return;

        using var db = AppDb.Create();
        foreach (var bill in db.UtilityBills.Where(b => ids.Contains(b.Id)).ToList())
            bill.IsArchived = archived;
        db.SaveChanges();

        LoadBills();
    }

    private void UpdateButtons()
    {
        var row = Selected;
        SplitButton.IsEnabled = row is { IsSplit: false };
        UndoButton.IsEnabled = row is { IsSplit: true };
        EditButton.IsEnabled = row is { IsSplit: false };
        DeleteButton.IsEnabled = row is { IsSplit: false };
        var rows = SelectedBills;
        ArchiveButton.IsEnabled = rows.Any(r => !r.IsArchived && r.Settled);
        UnarchiveButton.IsEnabled = rows.Any(r => r.IsArchived);
        RecalcButton.IsEnabled = row is { IsSplit: true };
    }

    // ---------- Loading ----------

    private void LoadBills()
    {
        if (SelectedMonth is not DateOnly start) return;
        var key = BillingHelpers.MonthKey(start);

        using var db = AppDb.Create();

        // Voided shares are filtered out automatically, so an undone split counts as not split.
        var splitIds = db.Charges
            .Where(c => c.UtilityBillId != null)
            .Select(c => c.UtilityBillId!.Value)
            .Distinct()
            .ToList();

        var bills = db.UtilityBills
            .Include(b => b.Room)
            .Where(b => b.BillingMonth == key)
            .ToList();

        // A bill is settled when it is split and none of its shares still has a balance.
        var unpaidBillIds = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.UtilityBillId != null)
            .ToList()
            .Where(c => c.Amount - c.Allocations.Sum(a => a.AmountApplied) > 0)
            .Select(c => c.UtilityBillId!.Value)
            .ToHashSet();

        var sharesByBill = LoadShares(db, bills, splitIds);

        _bills = bills
            .OrderBy(b => b.UtilityType)
            .Select(b =>
            {
                var split = splitIds.Contains(b.Id);
                var period = b.PeriodStart is DateOnly ps0 && b.PeriodEnd is DateOnly pe0
                    ? $"{Fmt.Date(ps0)} to {Fmt.Date(pe0)}"
                    : "the whole month";
                return new BillRow
                {
                    Id = b.Id,
                    UtilityType = b.UtilityType,
                    Scope = b.Room?.RoomName ?? "Whole house",
                    PeriodText = b.PeriodStart is DateOnly ps && b.PeriodEnd is DateOnly pe
                        ? $"{Fmt.Date(ps)} to {Fmt.Date(pe)}"
                        : "Whole month",
                    Total = b.TotalAmount,
                    Due = b.DueDate,
                    IsSplit = split,
                    IsArchived = b.IsArchived,
                    Settled = split && !unpaidBillIds.Contains(b.Id),
                    Shares = sharesByBill.TryGetValue(b.Id, out var lines) ? lines : new List<ShareLine>(),
                    ScopeNote = b.Room == null
                        ? $"Whole house: shared equally by every tenant who stayed in any room during {period}."
                        : $"{b.Room.RoomName} only: shared equally by every tenant who stayed in {b.Room.RoomName} during {period}."
                };
            })
            .ToList();

        var showArchived = ShowArchivedCheck.IsChecked == true;
        BillGrid.ItemsSource = _bills
            .Where(r => showArchived || !(r.IsArchived && r.Settled))
            .ToList();
        EmptyText.Visibility = BillGrid.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        bool Is(UtilityBill b, string name) => b.UtilityType.Trim().Equals(name, StringComparison.OrdinalIgnoreCase);

        var electricity = bills.Where(b => Is(b, "Electricity")).Sum(b => b.TotalAmount);
        var water = bills.Where(b => Is(b, "Water")).Sum(b => b.TotalAmount);
        var total = bills.Sum(b => b.TotalAmount);
        var monthName = start.ToString("MMMM", CultureInfo.CurrentCulture);

        ElectricityCard.Value = Fmt.Peso(electricity);
        ElectricityCard.Caption = $"{monthName} bill";
        WaterCard.Value = Fmt.Peso(water);
        WaterCard.Caption = $"{monthName} bill";
        OtherCard.Value = Fmt.Peso(total - electricity - water);
        OtherCard.Caption = "Internet and others";
        TotalCard.Value = Fmt.Peso(total);
        TotalCard.Caption = $"{Fmt.Count(bills.Count, "bill")} this month";
        TotalCard.Tone = "Info";

        UpdateButtons();
    }

    // Who each bill is billed to. A split bill lists the tenants who have a share (with the amount and whether it is paid);
    // a bill that is not split yet lists the tenants it would be split among, with the equal share each would get.
    private static Dictionary<int, List<ShareLine>> LoadShares(AppDbContext db, List<UtilityBill> bills, List<int> splitIds)
    {
        var result = new Dictionary<int, List<ShareLine>>();
        var billIds = bills.Select(b => b.Id).ToList();

        var shares = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Include(c => c.Occupancy).ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .Where(c => c.UtilityBillId != null && billIds.Contains(c.UtilityBillId.Value))
            .ToList();

        // Corrections made when shares were recalculated belong to the share they adjust.
        var shareIds = shares.Select(c => c.Id).ToList();
        var adjustments = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.AdjustsChargeId != null && shareIds.Contains(c.AdjustsChargeId.Value))
            .ToList();

        foreach (var group in shares.GroupBy(c => c.UtilityBillId!.Value))
        {
            var lines = new List<ShareLine>();
            foreach (var tenant in group.GroupBy(c => c.Occupancy.TenantId).OrderBy(g => g.First().Occupancy.Tenant.FullName, StringComparer.CurrentCultureIgnoreCase))
            {
                var mine = tenant.ToList();
                var linked = adjustments.Where(a => mine.Any(m => m.Id == a.AdjustsChargeId)).ToList();
                var amount = mine.Sum(c => c.Amount) + linked.Sum(a => a.Amount);
                var paid = mine.Sum(c => c.Allocations.Sum(x => x.AmountApplied)) + linked.Sum(a => a.Allocations.Sum(x => x.AmountApplied));
                var stay = mine.OrderByDescending(c => c.Id).First().Occupancy;

                lines.Add(new ShareLine
                {
                    Tenant = stay.Tenant.FullName,
                    Place = $"{stay.Bed.Room.RoomName} / {stay.Bed.BedLabel}",
                    Amount = amount,
                    StatusText = amount - paid <= 0 ? "Paid" : paid > 0 ? "Partial" : "Unpaid"
                });
            }
            result[group.Key] = lines;
        }

        // Bills not split yet: who would share them.
        var rooms = db.Rooms.AsNoTracking().ToDictionary(r => r.Id, r => r.RoomName);
        foreach (var bill in bills.Where(b => !splitIds.Contains(b.Id)))
        {
            if (UtilityRecalculation.PeriodOf(bill) is not BillPeriod period) continue;
            var stays = UtilityRecalculation.SharingStays(db, bill, period, null);
            if (stays.Count == 0) continue;

            var amounts = BillingRules.SplitEvenly(bill.TotalAmount, stays.Count);
            result[bill.Id] = stays
                .Select((s, i) => new ShareLine
                {
                    Tenant = s.TenantName,
                    Place = rooms.TryGetValue(s.RoomId, out var room) ? room : "",
                    Amount = amounts[i],
                    StatusText = "Not split"
                })
                .OrderBy(l => l.Tenant, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return result;
    }

    // ---------- Add, edit, delete ----------

    // Adding a bill has its own window. The bill is listed under the month selected on this page.
    private void AddBill_Click(object sender, RoutedEventArgs e)
    {
        if (MonthCombo.SelectedItem is not MonthOption month)
        {
            Say.Info("Select a month first.");
            return;
        }

        var dialog = new NewUtilityBillDialog(month.Start, month.Display);
        if (Owner != null) dialog.Owner = Owner;
        if (dialog.ShowDialog() == true) LoadBills();
    }

    private void EditBill_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsSplit) return;

        string fromText, toText;
        using (var load = AppDb.Create())
        {
            var current = load.UtilityBills.Find(row.Id)!;
            fromText = current.PeriodStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
            toText = current.PeriodEnd?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        }

        var v = FormDialog.Ask(Owner, $"Edit bill: {row.UtilityType}", "Save changes",
            new[]
            {
                ("Utility type", row.UtilityType),
                ("Total bill amount", row.Total.ToString("0.##", CultureInfo.CurrentCulture)),
                ("Due date (e.g. 2026-10-20)", (row.Due ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("Period from (e.g. 2026-09-15, blank for the whole month)", fromText),
                ("Period to (e.g. 2026-10-14, blank for the whole month)", toText)
            },
            vals =>
            {
                if (vals[0].Trim() == "") return "Enter the utility type.";
                if (!TryMoney(vals[1], out var amount) || amount <= 0) return "Enter the total bill amount as a number greater than zero.";
                if (!TryDate(vals[2], out _)) return "Enter the due date in the format 2026-10-20.";

                var hasFrom = vals[3].Trim() != "";
                var hasTo = vals[4].Trim() != "";
                if (hasFrom != hasTo) return "Fill in both period dates, or leave both blank for the whole month.";
                if (hasFrom)
                {
                    if (!TryDate(vals[3], out var pf) || !TryDate(vals[4], out var pt))
                        return "Enter both period dates in the format 2026-09-15.";
                    if (pt < pf) return "The period end date cannot be earlier than the start date.";
                    if (pt.DayNumber - pf.DayNumber > 366) return "The billing period cannot be longer than one year. Change the start or end date.";
                }

                var lower = vals[0].Trim().ToLower();
                using var check = AppDb.Create();
                var bill = check.UtilityBills.Find(row.Id)!;
                var duplicate = check.UtilityBills.Any(b => b.Id != bill.Id
                    && b.BillingMonth == bill.BillingMonth && b.RoomId == bill.RoomId
                    && b.UtilityType.ToLower() == lower);
                return duplicate ? "Another bill with the same utility, month and room already exists. Change the utility type." : null;
            },
            note: $"Applies to: {row.Scope}. To change what the bill applies to, delete it and add it again.");
        if (v == null) return;

        TryMoney(v[1], out var newTotal);
        TryDate(v[2], out var newDue);

        DateOnly? newFrom = null;
        DateOnly? newTo = null;
        if (v[3].Trim() != "" && TryDate(v[3], out var pf2) && TryDate(v[4], out var pt2))
        {
            newFrom = pf2;
            newTo = pt2;
        }

        // A short review of what changes. If nothing did, there is nothing to save.
        string PeriodShow(string text) => string.IsNullOrWhiteSpace(text) ? "whole month" : AuditWording.FriendlyDate(text);
        var reviewList = ConfirmWording.Changes(
            ("Utility type", row.UtilityType, v[0]),
            ("Total bill", Fmt.Peso(row.Total), Fmt.Peso(Math.Round(newTotal, 2))),
            ("Due date", Fmt.Date(row.Due ?? DateOnly.FromDateTime(DateTime.Today)), Fmt.Date(newDue)),
            ("Period from", PeriodShow(fromText), PeriodShow(v[3])),
            ("Period to", PeriodShow(toText), PeriodShow(v[4])));
        if (reviewList.Count == 0) return;
        if (!ConfirmActionDialog.Ask(Owner, "Edit bill",
                ConfirmWording.EditRecord($"the {row.UtilityType} bill", reviewList), "Save changes")) return;

        using var db = AppDb.Create();
        var entity = db.UtilityBills.Find(row.Id)!;
        entity.UtilityType = v[0].Trim();
        entity.TotalAmount = Math.Round(newTotal, 2);
        entity.DueDate = newDue;
        entity.PeriodStart = newFrom;
        entity.PeriodEnd = newTo;
        db.SaveChanges();

        LoadBills();
    }

    private void DeleteBill_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsSplit) return;

        using var db = AppDb.Create();

        // A bill whose split was undone keeps its (voided) shares as history, so it can't be deleted.
        if (db.Charges.IgnoreQueryFilters().Any(c => c.UtilityBillId == row.Id))
        {
            Say.Info("This bill cannot be deleted because it has charge history from a split that was undone. You can edit the bill instead.");
            return;
        }

        var confirm = ConfirmActionDialog.YesNo($"Delete the {row.UtilityType} bill of {Fmt.Peso(row.Total)}?\n\nThis cannot be undone.",
            "Delete bill", confirmText: "Delete");
        if (confirm != MessageBoxResult.Yes) return;

        db.UtilityBills.Remove(db.UtilityBills.Find(row.Id)!);
        db.SaveChanges();
        LoadBills();
    }

    // ---------- Split and undo split ----------

    private void SplitBill_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null) return;
        if (row.IsSplit)
        {
            Say.Info("This bill has already been split.");
            return;
        }

        using var db = AppDb.Create();
        var bill = db.UtilityBills.Find(row.Id)!;

        if (UtilityRecalculation.PeriodOf(bill) is not BillPeriod period)
        {
            Say.Info("The billing month of this bill could not be read, so the bill cannot be split.");
            return;
        }

        var stays = UtilityRecalculation.SharingStays(db, bill, period, null);
        if (stays.Count == 0)
        {
            Say.Info("No tenants stayed there during this bill's period, so the bill cannot be split. Check the billing period and the tenants' stay dates.");
            return;
        }

        var shares = BillingRules.SplitEvenly(bill.TotalAmount, stays.Count);

        var confirm = ConfirmActionDialog.YesNo(
            $"Split {Fmt.Peso(bill.TotalAmount)} ({bill.UtilityType}) among {Fmt.Count(stays.Count, "tenant")}?\n\n" +
            $"Everyone with at least one day between {Fmt.Date(period.Start)} and {Fmt.Date(period.End)} " +
            $"is charged an equal share of about {Fmt.Peso(shares[^1])} each.\n" +
            "You can undo the split later, as long as no payment has been applied to it.",
            "Confirm split", confirmText: "Split bill");
        if (confirm != MessageBoxResult.Yes) return;

        for (var i = 0; i < stays.Count; i++)
        {
            db.Charges.Add(new Charge
            {
                OccupancyId = stays[i].StayId,
                BillingMonth = bill.BillingMonth,
                ChargeType = ChargeType.Utility,
                Description = $"{bill.UtilityType} share",
                Amount = shares[i],   // leftover centavos are already in the first share
                DueDate = bill.DueDate ?? period.End,
                UtilityBillId = bill.Id
            });
        }

        AuditService.Record(db, "UtilityBill", bill.Id, null, "Split", null,
            $"{stays.Count} equal shares of {StayEditService.MoneyText(bill.TotalAmount)}", "Bill split");

        try
        {
            db.SaveChanges();   // all shares are saved together or not at all
        }
        catch (DbUpdateException)
        {
            Say.Info("The bill could not be split and nothing was saved. Its shares may already exist. Check the list and try again.");
        }

        LoadBills();
    }

    private void UndoSplit_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || !row.IsSplit) return;

        using (var check = AppDb.Create())
        {
            var shares = check.Charges.Include(c => c.Allocations)
                .Where(c => c.UtilityBillId == row.Id)
                .ToList();

            if (shares.Any(c => c.Allocations.Count > 0))
            {
                Say.Info("The split cannot be undone because payments have been applied to it.\n\n" +
                     "Void those payments on the Payments page first, then undo the split.");
                return;
            }
        }

        var v = FormDialog.Ask(Owner, $"Undo split: {row.UtilityType}", "Undo split",
            new[] { ("Reason", "") },
            vals => ReasonRules.Error(vals[0], "Enter a reason (at least 3 letters or digits)."),
            note: "This voids every tenant's share of this bill and returns the bill to Draft, so you can edit it and split it again. " +
                  "The voided shares remain in Billing Charges when Show voided is ticked.");
        if (v == null) return;

        using var db = AppDb.Create();
        var charges = db.Charges.Include(c => c.Allocations)
            .Where(c => c.UtilityBillId == row.Id)
            .ToList();

        if (charges.Any(c => c.Allocations.Count > 0))
        {
            Say.Info("A payment was just applied to one of these shares, so the split cannot be undone. Void that payment first, then try again.");
            LoadBills();
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var charge in charges)
        {
            charge.IsVoided = true;
            charge.VoidReason = "Split undone: " + v[0].Trim();
            charge.VoidedOn = today;
        }

        AuditService.Record(db, "UtilityBill", row.Id, null, "Split undone", null,
         $"{Fmt.Count(charges.Count, "share")} voided", v[0].Trim());

        db.SaveChanges();   // all shares are voided together or not at all

        LoadBills();
    }

    private void Recalculate_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || !row.IsSplit) return;

        using var db = AppDb.Create();
        var bill = db.UtilityBills.Include(b => b.Room).First(b => b.Id == row.Id);
        var plan = UtilityRecalculation.Plan(db, bill, null);

        if (plan == null || plan.Items.Count == 0)
        {
            Say.Info("Every tenant's share already matches who stayed during this bill's period. Nothing needs to change.");
            return;
        }

        if (!ImpactDialog.Confirm(Owner, plan.Label, plan.ToText())) return;

        var v = FormDialog.Ask(Owner, "Recalculate shares", "Apply changes",
            new[] { ("Reason", "") },
            vals => ReasonRules.Error(vals[0], "Enter a reason (at least 3 letters or digits)."),
            note: "The reason is saved in the change history.");
        if (v == null) return;

        List<string> done;
        try
        {
            done = UtilityRecalculation.Apply(db, plan, v[0].Trim());
            db.SaveChanges();   // all changes are saved together or not at all
        }
        catch (DbUpdateException)
        {
            Say.Info("The shares could not be updated, so nothing was saved. Please try again.");
            LoadBills();
            return;
        }

        LoadBills();
        Say.Info("Shares updated:\n\n" + string.Join("\n\n", done));
    }

}