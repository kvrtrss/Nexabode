using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class PaymentsPage : Page
{
    public class TenantOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class OpenChargeRow
    {
        public int Id { get; set; }
        public string BillingMonth { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateOnly DueDate { get; set; }
        public string DueText => DueDate.ToString("MMM d, yyyy");
        public string Status { get; set; } = "";
    }

    public class PaymentRow
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public string DateText => Date.ToString("MMM d, yyyy");
        public string Tenant { get; set; } = "";
        public decimal Amount { get; set; }
        public string Method { get; set; } = "";
        public string Reference { get; set; } = "";
        public string? Remarks { get; set; }
        public string Details { get; set; } = "";
        public bool IsVoided { get; set; }
        public string Status => IsVoided ? "Voided" : "Recorded";
    }

    public class BalanceRow
    {
        public string Tenant { get; set; } = "";
        public decimal Billed { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public decimal Overdue { get; set; }
    }

    private bool _ready;

    public PaymentsPage()
    {
        InitializeComponent();
        PayDatePicker.SelectedDate = DateTime.Today;
        MethodCombo.SelectedIndex = 0;
        Loaded += (_, _) =>
        {
            _ready = true;
            LoadTenants();
            RefreshAll();
        };
    }

    // ---------- Helpers ----------

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private Window? Owner => Window.GetWindow(this);

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private int? SelectedTenantId => (TenantCombo.SelectedItem as TenantOption)?.Id;

    private PaymentRow? SelectedPayment => PaymentGrid.SelectedItem as PaymentRow;

    private void RefreshAll()
    {
        LoadStats();
        LoadHistory();
        LoadAllBalances();
        LoadTenantData();
    }

    private void LoadTenants()
    {
        var keep = SelectedTenantId;

        using var db = new AppDbContext();
        var tenants = db.Tenants.OrderBy(t => t.FullName).ToList()
            .Select(t => new TenantOption { Id = t.Id, Name = t.FullName })
            .ToList();

        TenantCombo.ItemsSource = tenants;
        if (keep != null)
            TenantCombo.SelectedItem = tenants.FirstOrDefault(t => t.Id == keep);
    }

    private void TenantCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) LoadTenantData();
    }

    private void SelectedRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_ready) Tabs.SelectedIndex = 1;   // jump to the Unpaid charges tab so a charge can be chosen
    }

    // ---------- Stat cards ----------

    private void LoadStats()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        using var db = new AppDbContext();

        var payments = db.Payments.ToList().Where(p => !p.IsVoided).ToList();
        var thisMonth = payments.Where(p => p.PaymentDate >= monthStart).ToList();
        var todays = payments.Where(p => p.PaymentDate == today).ToList();

        MonthCard.Value = Fmt.Peso(thisMonth.Sum(p => p.Amount));
        MonthCard.Caption = $"{thisMonth.Count} payment(s)";
        MonthCard.Tone = "Success";

        TodayCard.Value = Fmt.Peso(todays.Sum(p => p.Amount));
        TodayCard.Caption = todays.Count == 0 ? "No payments today" : $"{todays.Count} payment(s)";
        TodayCard.Tone = "Info";

        var open = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy)
            .ToList()
            .Select(c => new
            {
                c.Occupancy.TenantId,
                c.DueDate,
                Balance = c.Amount - c.Allocations.Sum(a => a.AmountApplied)
            })
            .Where(x => x.Balance > 0)
            .ToList();

        var overdue = open.Where(x => x.DueDate < today).ToList();

        OutstandingCard.Value = Fmt.Peso(open.Sum(x => x.Balance));
        OutstandingCard.Caption = open.Count == 0
            ? "Everyone is settled"
            : $"{open.Select(x => x.TenantId).Distinct().Count()} tenant(s) owe money";
        OutstandingCard.Tone = open.Count == 0 ? "Success" : "Warning";

        OverdueCard.Value = Fmt.Peso(overdue.Sum(x => x.Balance));
        OverdueCard.Caption = overdue.Count == 0
            ? "Nothing overdue"
            : $"{overdue.Select(x => x.TenantId).Distinct().Count()} tenant(s) overdue";
        OverdueCard.Tone = overdue.Count == 0 ? "Success" : "Danger";
    }

    // ---------- Payment history ----------

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) LoadHistory();
    }

    private void ShowVoided_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) LoadHistory();
    }

    private void LoadHistory()
    {
        var query = SearchBox.Text.Trim();
        var showVoided = ShowVoidedCheck.IsChecked == true;

        using var db = new AppDbContext();
        var rows = db.Payments
            .Include(p => p.Tenant)
            .ToList()
            .Where(p => showVoided || !p.IsVoided)
            .Select(p => new PaymentRow
            {
                Id = p.Id,
                Date = p.PaymentDate,
                Tenant = p.Tenant.FullName,
                Amount = p.Amount,
                Method = p.Method ?? "",
                Reference = p.ReferenceNo ?? "",
                Remarks = p.Remarks,
                IsVoided = p.IsVoided,
                Details = p.IsVoided
                    ? $"Voided: {p.VoidReason}"
                    : p.Remarks ?? ""
            })
            .Where(r => query == ""
                || r.Tenant.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.Method.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.Reference.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)
            .ToList();

        PaymentGrid.ItemsSource = rows;
        UpdateHistoryButtons();
    }

    private void PaymentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHistoryButtons();

    private void UpdateHistoryButtons()
    {
        var selected = SelectedPayment;
        EditButton.IsEnabled = selected is { IsVoided: false };
        VoidButton.IsEnabled = selected is { IsVoided: false };
    }

    private void EditPayment_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedPayment;
        if (row == null || row.IsVoided) return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        var v = FormDialog.Ask(Owner, $"Edit payment - {row.Tenant}", "Save changes",
            new[]
            {
                ("Payment date (e.g. 2026-10-14)", row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("Method", row.Method),
                ("Reference no. (optional)", row.Reference),
                ("Remarks (optional)", row.Remarks ?? "")
            },
            vals =>
            {
                if (!DateOnly.TryParse(vals[0].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                    return "Enter the date like 2026-10-14.";
                if (date > today) return "The payment date can't be in the future.";
                return null;
            },
            note: $"Amount: {Fmt.Peso(row.Amount)}. The amount can't be edited. If it is wrong, void this payment and record a new one.");
        if (v == null) return;

        DateOnly.TryParse(v[0].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var newDate);

        using var db = new AppDbContext();
        var payment = db.Payments.Find(row.Id)!;
        payment.PaymentDate = newDate;
        payment.Method = Blank(v[1]);
        payment.ReferenceNo = Blank(v[2]);
        payment.Remarks = Blank(v[3]);
        db.SaveChanges();

        RefreshAll();
    }

    private void VoidPayment_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedPayment;
        if (row == null || row.IsVoided) return;

        var v = FormDialog.Ask(Owner, $"Void payment - {row.Tenant}", "Void payment",
            new[] { ("Reason for voiding", "") },
            vals => vals[0].Trim() == "" ? "Please enter a reason." : null,
            note: $"This cancels the payment of {Fmt.Peso(row.Amount)} from {row.DateText}. The charges it paid become unpaid again, " +
                  "and the payment stays in the history marked Voided. This can't be undone.");
        if (v == null) return;

        using var db = new AppDbContext();
        var payment = db.Payments
            .Include(p => p.Allocations).ThenInclude(a => a.Charge)
            .First(p => p.Id == row.Id);

        if (payment.IsVoided) { RefreshAll(); return; }

        // Remember what the payment had paid, then free those charges.
        var applied = string.Join("; ", payment.Allocations.Select(a =>
            $"{a.Charge.Description ?? a.Charge.ChargeType.ToString()} ({a.Charge.BillingMonth}) {Fmt.Peso(a.AmountApplied)}"));

        db.PaymentAllocations.RemoveRange(payment.Allocations);
        payment.IsVoided = true;
        payment.VoidReason = v[0].Trim() + (applied == "" ? "" : $" (was applied to: {applied})");
        payment.VoidedOn = DateOnly.FromDateTime(DateTime.Today);
        db.SaveChanges();   // everything is saved together or not at all

        RefreshAll();
    }

    // ---------- Selected tenant: summary and unpaid charges ----------

    // Charges of one tenant that still have a balance, oldest due date first.
    private static List<OpenChargeRow> GetOpenCharges(AppDbContext db, int tenantId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.Occupancy.TenantId == tenantId)
            .ToList()
            .Select(c =>
            {
                var paid = c.Allocations.Sum(a => a.AmountApplied);
                return new OpenChargeRow
                {
                    Id = c.Id,
                    BillingMonth = c.BillingMonth,
                    Description = c.Description ?? c.ChargeType.ToString(),
                    Amount = c.Amount,
                    Paid = paid,
                    Balance = c.Amount - paid,
                    DueDate = c.DueDate,
                    Status = c.DueDate < today ? "Overdue" : paid > 0 ? "Partial" : "Unpaid"
                };
            })
            .Where(r => r.Balance > 0)
            .OrderBy(r => r.DueDate).ThenBy(r => r.Id)
            .ToList();
    }

    private void LoadTenantData()
    {
        if (SelectedTenantId is not int tenantId)
        {
            ChargeGrid.ItemsSource = null;
            SummaryText.Text = "Select a tenant to begin.";
            return;
        }

        using var db = new AppDbContext();

        var open = GetOpenCharges(db, tenantId);
        ChargeGrid.ItemsSource = open;

        var allCharges = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.Occupancy.TenantId == tenantId)
            .ToList();
        var billed = allCharges.Sum(c => c.Amount);
        var paid = allCharges.Sum(c => c.Allocations.Sum(a => a.AmountApplied));
        var overdue = open.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        SummaryText.Text = $"Balance {Fmt.Peso(billed - paid)}  -  Overdue {Fmt.Peso(overdue)}";
    }

    private void LoadAllBalances()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();
        var charges = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .ToList();

        BalanceGrid.ItemsSource = charges
            .GroupBy(c => c.Occupancy.Tenant)
            .Select(g => new BalanceRow
            {
                Tenant = g.Key.FullName,
                Billed = g.Sum(c => c.Amount),
                Paid = g.Sum(c => c.Allocations.Sum(a => a.AmountApplied)),
                Balance = g.Sum(c => c.Amount - c.Allocations.Sum(a => a.AmountApplied)),
                Overdue = g.Where(c => c.DueDate < today)
                           .Sum(c => Math.Max(0, c.Amount - c.Allocations.Sum(a => a.AmountApplied)))
            })
            .Where(r => r.Balance > 0)
            .OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Balance)
            .ToList();
    }

    // ---------- Record a payment ----------

    private void RecordPayment_Click(object sender, RoutedEventArgs e)
    {
        if (TenantCombo.SelectedItem is not TenantOption tenant)
        {
            Tell("Select a tenant first.");
            return;
        }
        if (PayDatePicker.SelectedDate is not DateTime payDate)
        {
            Tell("Select the payment date.");
            return;
        }
        if (DateOnly.FromDateTime(payDate) > DateOnly.FromDateTime(DateTime.Today))
        {
            Tell("The payment date can't be in the future.");
            return;
        }
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            Tell("Enter a valid payment amount greater than zero.");
            return;
        }
        amount = Math.Round(amount, 2);

        using var db = new AppDbContext();
        var open = GetOpenCharges(db, tenant.Id);
        var totalBalance = open.Sum(r => r.Balance);

        if (open.Count == 0)
        {
            Tell("This tenant has no unpaid charges.");
            return;
        }
        if (amount > totalBalance)
        {
            Tell($"The amount ({Fmt.Peso(amount)}) is more than the tenant's balance ({Fmt.Peso(totalBalance)}).\n" +
                 "Advance payments are not supported yet.");
            return;
        }

        var allocations = new List<PaymentAllocation>();

        if (SelectedRadio.IsChecked == true)
        {
            if (ChargeGrid.SelectedItem is not OpenChargeRow chosen)
            {
                Tell("Click a charge in the Unpaid charges tab first, or choose \"oldest charges first\".");
                Tabs.SelectedIndex = 1;
                return;
            }
            var fresh = open.FirstOrDefault(r => r.Id == chosen.Id);
            if (fresh == null)
            {
                Tell("That charge is already fully paid.");
                LoadTenantData();
                return;
            }
            if (amount > fresh.Balance)
            {
                Tell($"That charge only has {Fmt.Peso(fresh.Balance)} left. Lower the amount, " +
                     "or choose \"oldest charges first\".");
                return;
            }
            allocations.Add(new PaymentAllocation { ChargeId = fresh.Id, AmountApplied = amount });
        }
        else
        {
            var remaining = amount;
            foreach (var charge in open)
            {
                if (remaining <= 0) break;
                var apply = Math.Min(remaining, charge.Balance);
                allocations.Add(new PaymentAllocation { ChargeId = charge.Id, AmountApplied = apply });
                remaining -= apply;
            }
        }

        var confirm = MessageBox.Show(
            $"Record a payment of {Fmt.Peso(amount)} from {tenant.Name}?\n\nIt will be applied to {allocations.Count} charge(s).",
            "Confirm payment", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        db.Payments.Add(new Payment
        {
            TenantId = tenant.Id,
            PaymentDate = DateOnly.FromDateTime(payDate),
            Amount = amount,
            Method = Blank(MethodCombo.Text),
            ReferenceNo = Blank(ReferenceBox.Text),
            Remarks = Blank(RemarksBox.Text),
            Allocations = allocations
        });
        db.SaveChanges();   // payment and its allocations are saved together or not at all

        AmountBox.Text = ReferenceBox.Text = RemarksBox.Text = "";
        OldestRadio.IsChecked = true;
        Tabs.SelectedIndex = 0;
        RefreshAll();
    }
}