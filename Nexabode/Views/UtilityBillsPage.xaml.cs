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

        db.UtilityBills.Add(new UtilityBill
        {
            UtilityType = type,
            BillingMonth = key,
            TotalAmount = Math.Round(total, 2),
            RoomId = scope.RoomId,
            DueDate = DateOnly.FromDateTime(dueDt)
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
        LoadBills();
    }

    private void EditBill_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsSplit) return;

        var v = FormDialog.Ask(Owner, $"Edit bill - {row.UtilityType}", "Save changes",
            new[]
            {
                ("Utility type", row.UtilityType),
                ("Total bill amount", row.Total.ToString("0.##", CultureInfo.CurrentCulture)),
                ("Due date (e.g. 2026-10-20)", (row.Due ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            },
            vals =>
            {
                if (vals[0].Trim() == "") return "Please enter the utility type.";
                if (!TryMoney(vals[1], out var amount) || amount <= 0) return "Enter the total as a number greater than zero.";
                if (!TryDate(vals[2], out _)) return "Enter the due date like 2026-10-20.";

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

        using var db = new AppDbContext();
        var entity = db.UtilityBills.Find(row.Id)!;
        entity.UtilityType = v[0].Trim();
        entity.TotalAmount = Math.Round(newTotal, 2);
        entity.DueDate = newDue;
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
        if (SelectedMonth is not DateOnly start) return;

        var end = start.AddMonths(1).AddDays(-1);

        using var db = new AppDbContext();
        var bill = db.UtilityBills.Find(row.Id)!;

        var staysInScope = db.Occupancies
            .Include(o => o.Bed)
            .Where(o => o.MoveInDate <= end && (o.MoveOutDate == null || o.MoveOutDate >= start))
            .ToList()
            .Where(o => bill.RoomId == null || o.Bed.RoomId == bill.RoomId)
            .ToList();

        // One share per tenant: a tenant with two stays in scope (a bed change) is not charged twice.
        var stays = BillingRules.OneStayPerTenant(staysInScope);

        if (stays.Count == 0)
        {
            Tell("No tenants were staying there during this month, so the bill can't be split.");
            return;
        }

        var shares = BillingRules.SplitEvenly(bill.TotalAmount, stays.Count);

        var confirm = MessageBox.Show(
            $"Split {Fmt.Peso(bill.TotalAmount)} ({bill.UtilityType}) among {stays.Count} tenant(s)?\n\n" +
            $"Each pays about {Fmt.Peso(shares[^1])}.\nYou can undo the split later if nothing has been paid on it.",
            "Confirm split", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        for (var i = 0; i < stays.Count; i++)
        {
            db.Charges.Add(new Charge
            {
                OccupancyId = stays[i].Id,
                BillingMonth = bill.BillingMonth,
                ChargeType = ChargeType.Utility,
                Description = $"{bill.UtilityType} share",
                Amount = shares[i],   // leftover centavos are already in the first share
                DueDate = bill.DueDate ?? end,
                UtilityBillId = bill.Id
            });
        }

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
        db.SaveChanges();   // all shares are voided together or not at all

        LoadBills();
    }
}