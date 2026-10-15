using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class BillingPage : Page
{
    public class ChargeRow
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = "";
        public string Bed { get; set; } = "";
        public string Description { get; set; } = "";
        public ChargeType Type { get; set; }
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateOnly DueDate { get; set; }
        public string DueText => DueDate.ToString("MMM d, yyyy");
        public string Status { get; set; } = "";
        public bool IsVoided { get; set; }
        public string Note { get; set; } = "";
    }

    private bool _ready;

    public BillingPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    // ---------- Helpers ----------

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private Window? Owner => Window.GetWindow(this);

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out date);

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

    private ChargeRow? Selected => ChargeGrid.SelectedItem as ChargeRow;

    private void Setup()
    {
        var months = BillingHelpers.BuildMonths();
        MonthCombo.ItemsSource = months;
        _ready = true;
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || SelectedMonth is not DateOnly start) return;
        RentDuePicker.SelectedDate = start.AddDays(4).ToDateTime(TimeOnly.MinValue);   // the 5th
        LoadCharges();
    }

    private void ShowVoided_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) LoadCharges();
    }

    private void ChargeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        var row = Selected;
        EditButton.IsEnabled = row is { IsVoided: false };
        VoidButton.IsEnabled = row is { IsVoided: false };
    }

    // ---------- Loading ----------

    private void LoadCharges()
    {
        if (SelectedMonth is not DateOnly start) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var key = BillingHelpers.MonthKey(start);
        var showVoided = ShowVoidedCheck.IsChecked == true;

        using var db = new AppDbContext();
        var charges = db.Charges.IgnoreQueryFilters()   // look past the voided-charge filter
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Include(c => c.Occupancy).ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .Where(c => c.BillingMonth == key && (showVoided || !c.IsVoided))
            .ToList();

        var rows = charges
            .Select(c =>
            {
                var paid = c.Allocations.Sum(a => a.AmountApplied);
                var balance = c.Amount - paid;
                return new ChargeRow
                {
                    Id = c.Id,
                    Tenant = c.Occupancy.Tenant.FullName,
                    Bed = $"{c.Occupancy.Bed.Room.RoomName} / {c.Occupancy.Bed.BedLabel}",
                    Description = c.Description ?? c.ChargeType.ToString(),
                    Type = c.ChargeType,
                    Amount = c.Amount,
                    Paid = paid,
                    Balance = balance,
                    DueDate = c.DueDate,
                    IsVoided = c.IsVoided,
                    Note = c.IsVoided ? $"Voided: {c.VoidReason}" : "",
                    Status = BillingRules.ChargeStatus(c.Amount, paid, c.DueDate, today, c.IsVoided)
                };
            })
            .OrderBy(r => r.Tenant).ThenBy(r => r.Description)
            .ToList();

        ChargeGrid.ItemsSource = rows;

        // Totals ignore voided charges.
        var live = rows.Where(r => !r.IsVoided).ToList();
        var billed = live.Sum(r => r.Amount);
        var paidTotal = live.Sum(r => r.Paid);
        var overdue = live.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        BilledCard.Value = Fmt.Peso(billed);
        BilledCard.Caption = $"{live.Count} charge(s)";

        PaidCard.Value = Fmt.Peso(paidTotal);
        PaidCard.Caption = billed == 0 ? "Nothing billed yet" : $"{paidTotal * 100 / billed:0}% collected";
        PaidCard.Tone = "Success";

        BalanceCard.Value = Fmt.Peso(billed - paidTotal);
        BalanceCard.Caption = "Still to collect";
        BalanceCard.Tone = "Warning";

        OverdueCard.Value = Fmt.Peso(overdue);
        OverdueCard.Caption = overdue > 0 ? "Past due date" : "Nothing overdue";
        OverdueCard.Tone = overdue > 0 ? "Danger" : "Success";

        UpdateButtons();
    }

    // ---------- Edit and void ----------

    private void EditCharge_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsVoided) return;

        var canEditAmount = row.Type != ChargeType.Utility && row.Paid == 0;

        var fields = new List<(string Label, string Value)>
        {
            ("Description", row.Description),
            ("Due date (e.g. 2026-10-05)", row.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        };
        if (canEditAmount)
            fields.Add(("Amount", row.Amount.ToString("0.##", CultureInfo.CurrentCulture)));

        var note = row.Type == ChargeType.Utility
            ? $"Amount: {Fmt.Peso(row.Amount)}. A utility share can't be edited here. To change it, undo the split on the Utility Bills page and split again."
            : row.Paid > 0
                ? $"Amount: {Fmt.Peso(row.Amount)}. The amount is locked because payments have been applied. Void the payment first if the amount is wrong."
                : null;

        var v = FormDialog.Ask(Owner, $"Edit charge - {row.Tenant}", "Save changes", fields.ToArray(),
            vals =>
            {
                if (vals[0].Trim() == "") return "Please enter a description.";
                if (!TryDate(vals[1], out _)) return "Enter the due date like 2026-10-05.";
                if (canEditAmount && (!TryMoney(vals[2], out var amount) || amount <= 0))
                    return "Enter the amount as a number greater than zero.";
                return null;
            }, note);
        if (v == null) return;

        using var db = new AppDbContext();
        var charge = db.Charges.First(c => c.Id == row.Id);
        charge.Description = Blank(v[0]);
        TryDate(v[1], out var due);
        charge.DueDate = due;
        if (canEditAmount)
        {
            TryMoney(v[2], out var amount);
            charge.Amount = Math.Round(amount, 2);
        }
        db.SaveChanges();

        LoadCharges();
    }

    private void VoidCharge_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsVoided) return;

        if (row.Paid > 0)
        {
            Tell("This charge has payments applied to it.\n\n" +
                 "Void those payments first (Payments page, Payment history), then void the charge.");
            return;
        }

        var note = $"This cancels the {row.Description.ToLower()} of {Fmt.Peso(row.Amount)} for {row.Tenant}. " +
                   "It stays visible when you tick Show voided, and it no longer counts in any total. This can't be undone.";
        if (row.Type == ChargeType.Rent)
            note += " A voided rent charge is not created again when you generate rent.";
        if (row.Type == ChargeType.Utility)
            note += " Other tenants' shares of this bill are not changed.";

        var v = FormDialog.Ask(Owner, $"Void charge - {row.Tenant}", "Void charge",
            new[] { ("Reason for voiding", "") },
            vals => vals[0].Trim() == "" ? "Please enter a reason." : null,
            note);
        if (v == null) return;

        using var db = new AppDbContext();
        var charge = db.Charges.Include(c => c.Allocations).First(c => c.Id == row.Id);

        if (charge.Allocations.Count > 0)
        {
            Tell("A payment was just applied to this charge. Void that payment first.");
            LoadCharges();
            return;
        }

        charge.IsVoided = true;
        charge.VoidReason = v[0].Trim();
        charge.VoidedOn = DateOnly.FromDateTime(DateTime.Today);
        db.SaveChanges();

        LoadCharges();
    }

    // ---------- Generate rent ----------

    private void GenerateRent_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMonth is not DateOnly start)
        {
            Tell("Select a billing month first.");
            return;
        }
        if (RentDuePicker.SelectedDate is not DateTime dueDt)
        {
            Tell("Select a rent due date.");
            return;
        }

        var key = BillingHelpers.MonthKey(start);
        var end = start.AddMonths(1).AddDays(-1);
        var due = DateOnly.FromDateTime(dueDt);
        var prorate = ProrateCheck.IsChecked == true;

        using var db = new AppDbContext();
        var stays = db.Occupancies
            .Where(o => o.MoveInDate <= end && (o.MoveOutDate == null || o.MoveOutDate >= start))
            .ToList();

        if (stays.Count == 0)
        {
            Tell("No tenants were staying in that month, so there is nothing to bill.");
            return;
        }

        // A tenant with more than one stay in the month (for example, a bed change) is billed
        // by days for each stay, so they never pay the full rent twice.
        var multiStayTenants = stays
            .GroupBy(s => s.TenantId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var created = 0;
        var skipped = 0;
        var byDays = 0;

        foreach (var stay in stays)
        {
            // Voided charges still count, so a rent charge you voided is not created again.
            var alreadyBilled = db.Charges.IgnoreQueryFilters().Any(c => c.OccupancyId == stay.Id
                && c.BillingMonth == key && c.ChargeType == ChargeType.Rent);
            if (alreadyBilled) { skipped++; continue; }

            var splitIntoDays = prorate || multiStayTenants.Contains(stay.TenantId);
            var handover = stay.MoveOutDate is DateOnly outDay &&
                stays.Any(s => s.TenantId == stay.TenantId && s.Id != stay.Id && s.MoveInDate == outDay);

            var rent = BillingRules.CalculateRent(stay.AgreedRent, stay.MoveInDate, stay.MoveOutDate,
                                                  start, splitIntoDays, handover);
            if (rent == null) continue;

            if (rent.IsPartial && multiStayTenants.Contains(stay.TenantId)) byDays++;

            db.Charges.Add(new Charge
            {
                OccupancyId = stay.Id,
                BillingMonth = key,
                ChargeType = ChargeType.Rent,
                Description = rent.Description,
                Amount = rent.Amount,
                DueDate = due
            });
            created++;
        }

        db.SaveChanges();
        LoadCharges();

        var message = $"Rent charges created: {created}.\nAlready billed (skipped): {skipped}.";
        if (byDays > 0)
            message += $"\n\n{byDays} charge(s) were billed by days because the tenant had more than one stay this month.";
        Tell(message);
    }
}