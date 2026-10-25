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
    }

    private bool _ready; 
    private List<BillRow> _bills = new();

    public UtilityBillsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    // ---------- Helpers ----------

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

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
        using (var db = new AppDbContext())
        {
            var scopes = new List<ScopeOption> { new() { RoomId = null, Display = "Whole house" } };
            scopes.AddRange(db.Rooms.OrderBy(r => r.RoomName).ToList()
                .Select(r => new ScopeOption { RoomId = r.Id, Display = r.RoomName }));
            ScopeCombo.ItemsSource = scopes;
            ScopeCombo.SelectedIndex = 0;
        }
        UtilityTypeBox.SelectedIndex = 0;

        var months = BillingHelpers.BuildMonths();
        MonthCombo.ItemsSource = months;
        _ready = true;
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || SelectedMonth is not DateOnly start) return;
        BillDuePicker.SelectedDate = start.AddDays(19).ToDateTime(TimeOnly.MinValue);   // the 20th
        LoadBills();
    }

    private void BillGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

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
            Tell("There are no fully paid bills left to archive in this month.");
            return;
        }

        var month = (MonthCombo.SelectedItem as MonthOption)?.Display;
        var answer = MessageBox.Show(
            $"Archive {ids.Count} fully paid bill(s) for {month}?\n\n" +
            "They are only hidden from this list. Totals, reports and statements are not affected. " +
            "The bills' charges stay in Billing Charges until you archive them there.",
            "Archive settled bills", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        ArchiveBills(ids, true);
    }

    private void ArchiveBills(List<int> ids, bool archived)
    {
        if (ids.Count == 0) return;

        using var db = new AppDbContext();
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

        using var db = new AppDbContext();

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

        _bills = bills
            .OrderBy(b => b.UtilityType)
            .Select(b =>
            {
                var split = splitIds.Contains(b.Id);
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
                    Settled = split && !unpaidBillIds.Contains(b.Id)
                };
            })
            .ToList();

        var showArchived = ShowArchivedCheck.IsChecked == true;
        BillGrid.ItemsSource = _bills
            .Where(r => showArchived || !(r.IsArchived && r.Settled))
            .ToList();

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
        TotalCard.Caption = $"{bills.Count} bill(s) this month";
        TotalCard.Tone = "Info";

        UpdateButtons();
    }

    // ---------- Add, edit, delete ----------

    private void AddBill_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMonth is not DateOnly start)
        {
            Tell("Select a month first.");
            return;
        }

        var type = UtilityTypeBox.Text.Trim();
        if (type == "")
        {
            Tell("Enter the utility type (e.g. Electricity).");
            return;
        }
        if (!TryMoney(BillAmountBox.Text, out var total) || total <= 0)
        {
            Tell("Enter a valid total bill amount greater than zero.");
            return;
        }
        if (ScopeCombo.SelectedItem is not ScopeOption scope)
        {
            Tell("Choose whether the bill applies to the whole house or one room.");
            return;
        }
        if (BillDuePicker.SelectedDate is not DateTime dueDt)
        {
            Tell("Select a due date.");
            return;
        }

        // Optional billing period: both dates, or neither (then the bill covers the whole month).
        var fromSet = PeriodFromPicker.SelectedDate != null;
        var toSet = PeriodToPicker.SelectedDate != null;
        if (fromSet != toSet)
        {
            Tell("Pick both period dates, or leave both blank if the bill covers the whole month.");
            return;
        }

        DateOnly? periodStart = null;
        DateOnly? periodEnd = null;
        if (fromSet)
        {
            var from = DateOnly.FromDateTime(PeriodFromPicker.SelectedDate!.Value);
            var to = DateOnly.FromDateTime(PeriodToPicker.SelectedDate!.Value);
            if (to < from)
            {
                Tell("The billing period can't end before it starts.");
                return;
            }
            if (to.DayNumber - from.DayNumber > 366)
            {
                Tell("A billing period can be at most one year long.");
                return;
            }

            var monthEnd = start.AddMonths(1).AddDays(-1);
            if (to < start || from > monthEnd)
            {
                var ask = MessageBox.Show(
                    "This period does not touch the selected month, but the bill will be listed under it. Continue?",
                    "Billing period", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ask != MessageBoxResult.Yes) return;
            }

            periodStart = from;
            periodEnd = to;
        }

        var key = BillingHelpers.MonthKey(start);

        using var db = new AppDbContext();
        var lower = type.ToLower();
        var duplicate = db.UtilityBills.Any(b => b.BillingMonth == key
            && b.RoomId == scope.RoomId && b.UtilityType.ToLower() == lower);
        if (duplicate)
        {
            Tell("A bill for that utility, month, and room/house already exists.");
            return;
        }

        // The same utility and room must not be billed twice for the same days.
        var newPeriod = UtilityRules.EffectivePeriod(key, periodStart, periodEnd)!.Value;
        var overlapping = db.UtilityBills
            .Where(b => b.RoomId == scope.RoomId && b.UtilityType.ToLower() == lower)
            .ToList()
            .Where(b => UtilityRecalculation.PeriodOf(b) is BillPeriod p && UtilityRules.PeriodsOverlap(p, newPeriod))
            .ToList();
        if (overlapping.Count > 0)
        {
            var list = string.Join("\n", overlapping.Select(b =>
                UtilityRecalculation.PeriodOf(b) is BillPeriod p
                    ? $"- {b.BillingMonth}: {Fmt.Date(p.Start)} to {Fmt.Date(p.End)}"
                    : $"- {b.BillingMonth}"));
            var ask = MessageBox.Show(
                $"There is already a {type} bill for {scope.Display} covering some of the same days:\n\n{list}\n\n" +
                "Adding this one may bill those days twice. Add it anyway?",
                "Overlapping bills", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ask != MessageBoxResult.Yes) return;
        }

        db.UtilityBills.Add(new UtilityBill
        {
            UtilityType = type,
            BillingMonth = key,
            TotalAmount = Math.Round(total, 2),
            RoomId = scope.RoomId,
            DueDate = DateOnly.FromDateTime(dueDt),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        });

        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            Tell("This bill could not be saved, so nothing was saved. A bill for the same month, utility and room may already exist.");
            LoadBills();
            return;
        }

        BillAmountBox.Text = "";
        PeriodFromPicker.SelectedDate = null;
        PeriodToPicker.SelectedDate = null;
        LoadBills();
    }

    private void EditBill_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsSplit) return;

        string fromText, toText;
        using (var load = new AppDbContext())
        {
            var current = load.UtilityBills.Find(row.Id)!;
            fromText = current.PeriodStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
            toText = current.PeriodEnd?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        }

        var v = FormDialog.Ask(Owner, $"Edit bill - {row.UtilityType}", "Save changes",
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
                if (vals[0].Trim() == "") return "Please enter the utility type.";
                if (!TryMoney(vals[1], out var amount) || amount <= 0) return "Enter the total as a number greater than zero.";
                if (!TryDate(vals[2], out _)) return "Enter the due date like 2026-10-20.";

                var hasFrom = vals[3].Trim() != "";
                var hasTo = vals[4].Trim() != "";
                if (hasFrom != hasTo) return "Fill in both period dates, or leave both blank for the whole month.";
                if (hasFrom)
                {
                    if (!TryDate(vals[3], out var pf) || !TryDate(vals[4], out var pt))
                        return "Enter the period dates like 2026-09-15.";
                    if (pt < pf) return "The period can't end before it starts.";
                    if (pt.DayNumber - pf.DayNumber > 366) return "A billing period can be at most one year long.";
                }

                var lower = vals[0].Trim().ToLower();
                using var check = new AppDbContext();
                var bill = check.UtilityBills.Find(row.Id)!;
                var duplicate = check.UtilityBills.Any(b => b.Id != bill.Id
                    && b.BillingMonth == bill.BillingMonth && b.RoomId == bill.RoomId
                    && b.UtilityType.ToLower() == lower);
                return duplicate ? "Another bill with that utility, month, and room already exists." : null;
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

        using var db = new AppDbContext();
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

        using var db = new AppDbContext();

        // A bill whose split was undone keeps its (voided) shares as history, so it can't be deleted.
        if (db.Charges.IgnoreQueryFilters().Any(c => c.UtilityBillId == row.Id))
        {
            Tell("This bill has charge history (its split was undone), so it can't be deleted. Edit it instead.");
            return;
        }

        var confirm = MessageBox.Show($"Delete the {row.UtilityType} bill of {Fmt.Peso(row.Total)}?",
            "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
            Tell("This bill has already been split.");
            return;
        }

        using var db = new AppDbContext();
        var bill = db.UtilityBills.Find(row.Id)!;

        if (UtilityRecalculation.PeriodOf(bill) is not BillPeriod period)
        {
            Tell("This bill's month could not be read, so it can't be split.");
            return;
        }

        var stays = UtilityRecalculation.SharingStays(db, bill, period, null);
        if (stays.Count == 0)
        {
            Tell("No tenants were staying there during this bill's period, so the bill can't be split.");
            return;
        }

        var shares = BillingRules.SplitEvenly(bill.TotalAmount, stays.Count);

        var confirm = MessageBox.Show(
            $"Split {Fmt.Peso(bill.TotalAmount)} ({bill.UtilityType}) among {stays.Count} tenant(s)?\n\n" +
            $"Everyone with at least one day between {Fmt.Date(period.Start)} and {Fmt.Date(period.End)} " +
            $"pays an equal share, about {Fmt.Peso(shares[^1])} each.\n" +
            "You can undo the split later if nothing has been paid on it.",
            "Confirm split", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
            Tell("This bill could not be split, so nothing was saved. Its shares may already exist.");
        }

        LoadBills();
    }

    private void UndoSplit_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || !row.IsSplit) return;

        using (var check = new AppDbContext())
        {
            var shares = check.Charges.Include(c => c.Allocations)
                .Where(c => c.UtilityBillId == row.Id)
                .ToList();

            if (shares.Any(c => c.Allocations.Count > 0))
            {
                Tell("Some tenants have already paid part of this bill.\n\n" +
                     "Void those payments first (Payments page), then undo the split.");
                return;
            }
        }

        var v = FormDialog.Ask(Owner, $"Undo split - {row.UtilityType}", "Undo split",
            new[] { ("Reason", "") },
            vals => vals[0].Trim() == "" ? "Please enter a reason." : null,
            note: "This voids every tenant share of this bill. The bill goes back to Draft, so you can edit it and split again. " +
                  "The voided shares stay visible on Billing Charges under Show voided.");
        if (v == null) return;

        using var db = new AppDbContext();
        var charges = db.Charges.Include(c => c.Allocations)
            .Where(c => c.UtilityBillId == row.Id)
            .ToList();

        if (charges.Any(c => c.Allocations.Count > 0))
        {
            Tell("A payment was just applied to one of these shares. Void that payment first.");
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
         $"{charges.Count} share(s) voided", v[0].Trim());

        db.SaveChanges();   // all shares are voided together or not at all

        LoadBills();
    }

    private void Recalculate_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || !row.IsSplit) return;

        using var db = new AppDbContext();
        var bill = db.UtilityBills.Include(b => b.Room).First(b => b.Id == row.Id);
        var plan = UtilityRecalculation.Plan(db, bill, null);

        if (plan == null || plan.Items.Count == 0)
        {
            Tell("Every tenant's share already matches who stayed during this bill's period. Nothing needs to change.");
            return;
        }

        if (!ImpactDialog.Confirm(Owner, plan.Label, plan.ToText())) return;

        var v = FormDialog.Ask(Owner, "Recalculate shares", "Apply",
            new[] { ("Reason", "") },
            vals => vals[0].Trim() == "" ? "Please enter a reason." : null,
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
            Tell("The shares could not be updated, so nothing was saved. Please try again.");
            LoadBills();
            return;
        }

        LoadBills();
        Tell("Shares updated:\n\n" + string.Join("\n\n", done));
    }

}