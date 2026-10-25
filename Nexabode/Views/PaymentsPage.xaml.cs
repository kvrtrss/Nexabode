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
        public string Item { get; set; } = "";
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
        public bool IsArchived { get; set; }
        public string Status => IsVoided ? "Voided" : IsArchived ? "Archived" : "Recorded";
        public string PaidFor { get; set; } = "";
        public decimal Received { get; set; }
        public decimal Change { get; set; }
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

    public class PayForOption
    {
        public string Key { get; set; } = "";
        public string Display { get; set; } = "";
        public string Hint { get; set; } = "";
        public string? Item { get; set; }     // null = any item
        public bool Specific { get; set; }    // true = one charge picked in the Unpaid charges tab
    }

    private bool _loadingPayFor;
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

    private PaymentRow? SelectedPayment =>
    PaymentGrid.SelectedItems.Count == 1 ? PaymentGrid.SelectedItem as PaymentRow : null;

    private List<PaymentRow> SelectedPayments =>
        PaymentGrid.SelectedItems.OfType<PaymentRow>().ToList();

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
        if (!_ready) return;
        LoadTenantData();
        UpdateChangeHint();
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

    private void ShowArchived_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) LoadHistory();
    }

    private void Archive_Click(object sender, RoutedEventArgs e) => SetArchived(true);

    private void Unarchive_Click(object sender, RoutedEventArgs e) => SetArchived(false);

    private void SetArchived(bool archived)
    {
        var ids = SelectedPayments.Where(r => r.IsArchived != archived).Select(r => r.Id).ToList();
        if (ids.Count == 0) return;

        using var db = new AppDbContext();
        foreach (var payment in db.Payments.Where(p => ids.Contains(p.Id)).ToList())
            payment.IsArchived = archived;
        db.SaveChanges();

        LoadHistory();
    }

    private void LoadHistory()
    {
        var query = SearchBox.Text.Trim();
        var showVoided = ShowVoidedCheck.IsChecked == true;
        var showArchived = ShowArchivedCheck.IsChecked == true;
        using var db = new AppDbContext();
        var rows = db.Payments
            .Include(p => p.Tenant)
            .Include(p => p.Allocations).ThenInclude(a => a.Charge).ThenInclude(c => c.UtilityBill)
            .ToList()
            .Where(p => (showVoided || !p.IsVoided) && (showArchived || !p.IsArchived))
            .Select(p => new PaymentRow
            {
                Id = p.Id,
                Date = p.PaymentDate,
                Tenant = p.Tenant.FullName,
                Amount = p.Amount,
                Received = p.AmountReceived ?? p.Amount,
                Change = (p.AmountReceived ?? p.Amount) - p.Amount,
                Method = p.Method ?? "",
                Reference = p.ReferenceNo ?? "",
                Remarks = p.Remarks,
                IsVoided = p.IsVoided,
                IsArchived = p.IsArchived,
                PaidFor = string.Join(", ", p.Allocations
                    .Select(a => BillingHelpers.ItemOf(a.Charge))
                    .Distinct()
                    .OrderBy(i => i == "Rent" ? 0 : 1).ThenBy(i => i)),
                Details = p.IsVoided ? $"Voided: {p.VoidReason}" : p.Remarks ?? ""
            })
            .Where(r => query == ""
                || r.Tenant.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.Method.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.Reference.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.PaidFor.Contains(query, StringComparison.OrdinalIgnoreCase))
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

        var rows = SelectedPayments;
        ArchiveButton.IsEnabled = rows.Any(r => !r.IsArchived);
        UnarchiveButton.IsEnabled = rows.Any(r => r.IsArchived);
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
                  "and the payment stays in the history marked Voided (tick Show voided to see it). This can't be undone." +
                  (row.Change > 0 ? $"\n\nThe tenant handed over {Fmt.Peso(row.Received)} and {Fmt.Peso(row.Change)} was returned as change." : ""));
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

    private void LoadTenantData()
    {
        if (SelectedTenantId is not int tenantId)
        {
            ChargeGrid.ItemsSource = null;
            _loadingPayFor = true;
            PayForCombo.ItemsSource = null;
            _loadingPayFor = false;
            PayForHint.Text = "";
            SummaryText.Text = "Select a tenant to begin.";
            return;
        }

        using var db = new AppDbContext();

        var open = GetOpenCharges(db, tenantId);
        ChargeGrid.ItemsSource = open;
        LoadPayFor(open);

        var allCharges = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.Occupancy.TenantId == tenantId)
            .ToList();
        var billed = allCharges.Sum(c => c.Amount);
        var paid = allCharges.Sum(c => c.Allocations.Sum(a => a.AmountApplied));
        var overdue = open.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        SummaryText.Text = $"Balance {Fmt.Peso(billed - paid)}  -  Overdue {Fmt.Peso(overdue)}";
    }

    // The "paying for" choices: any item, one item (Rent, Electricity, ...), or one specific charge.
    private void LoadPayFor(List<OpenChargeRow> open)
    {
        var keep = (PayForCombo.SelectedItem as PayForOption)?.Key;

        var options = new List<PayForOption>
    {
        new() { Key = "oldest", Display = "Oldest charges first (any item)",
                Hint = "Pays the oldest unpaid charge first, then the next one. Any extra is shown as change to return." }
    };

        foreach (var group in open.GroupBy(r => r.Item)
                                  .OrderBy(g => g.Key == "Rent" ? 0 : 1).ThenBy(g => g.Key))
        {
            var due = group.Sum(r => r.Balance);
            options.Add(new PayForOption
            {
                Key = "item:" + group.Key,
                Item = group.Key,
                Display = $"{group.Key}  -  {Fmt.Peso(due)} unpaid",
                Hint = $"Pays only {group.Key} charges, oldest first. Up to {Fmt.Peso(due)}; any extra is change to return."
            });
        }

        options.Add(new PayForOption
        {
            Key = "specific",
            Specific = true,
            Display = "One specific charge (pick it in Unpaid charges)",
            Hint = "Click a charge in the Unpaid charges tab, then record the payment."
        });

        _loadingPayFor = true;
        PayForCombo.ItemsSource = options;
        PayForCombo.SelectedItem = options.FirstOrDefault(o => o.Key == keep) ?? options[0];
        _loadingPayFor = false;

        PayForHint.Text = (PayForCombo.SelectedItem as PayForOption)?.Hint ?? "";
    }

    private void PayFor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _loadingPayFor) return;

        if (PayForCombo.SelectedItem is not PayForOption option)
        {
            PayForHint.Text = "";
            return;
        }

        PayForHint.Text = option.Hint;
        if (option.Specific) Tabs.SelectedIndex = 1;   // jump to Unpaid charges so a charge can be picked
        UpdateChangeHint();
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

    // Works out how a payment would be applied, using the balances in the database right now.
    // Charges of one tenant that still have a balance, oldest due date first.
    private static List<OpenChargeRow> GetOpenCharges(AppDbContext db, int tenantId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return db.Charges
            .Include(c => c.UtilityBill)
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
                    Item = BillingHelpers.ItemOf(c),
                    Description = c.Description ?? c.ChargeType.ToString(),
                    Amount = c.Amount,
                    Paid = paid,
                    Balance = c.Amount - paid,
                    DueDate = c.DueDate,
                    Status = BillingRules.ChargeStatus(c.Amount, paid, c.DueDate, today)
                };
            })
            .Where(r => r.Balance > 0)
            .OrderBy(r => r.DueDate).ThenBy(r => r.Id)
            .ToList();
    }

    // Works out how a payment would be applied, using the balances in the database right now.
    // Paying more than is owed is not an error: the extra is returned as change.
    private static (List<PaymentAllocation>? Allocations, decimal Applied, decimal Change, string? Error) PlanAllocations(
        AppDbContext db, int tenantId, decimal amount, int? chosenChargeId, string? item)
    {
        var open = GetOpenCharges(db, tenantId);
        if (open.Count == 0)
            return (null, 0, 0, "This tenant has no unpaid charges, so there is nothing to apply this payment to.\n\n" +
                                "Nexabode does not hold advance payments, so nothing was recorded.");

        List<OpenChargeRow> scope;
        if (chosenChargeId is int chargeId)
        {
            var fresh = open.FirstOrDefault(r => r.Id == chargeId);
            if (fresh == null) return (null, 0, 0, "That charge is already fully paid.");
            scope = new List<OpenChargeRow> { fresh };
        }
        else
        {
            scope = item == null ? open : open.Where(r => r.Item == item).ToList();
            if (scope.Count == 0) return (null, 0, 0, $"This tenant has no unpaid {item} charges.");
        }

        var settlement = PaymentRules.Settle(amount, scope.Select(c => (c.Id, c.Balance)));
        var allocations = settlement.Allocations
            .Select(a => new PaymentAllocation { ChargeId = a.ChargeId, AmountApplied = a.Applied })
            .ToList();

        return (allocations, settlement.Applied, settlement.Change, null);
    }

    private void AmountBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateChangeHint();

    private void ChargeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateChangeHint();

    // Shows, while typing, how much of the amount will be applied and how much is change to return.
    private void UpdateChangeHint()
    {
        if (!_ready) return;
        ChangeHint.Visibility = Visibility.Collapsed;

        if (SelectedTenantId is not int tenantId) return;
        if (PayForCombo.SelectedItem is not PayForOption option) return;
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var typed) || typed <= 0)
            return;

        int? chosenChargeId = null;
        string? item = null;
        if (option.Specific)
        {
            if (ChargeGrid.SelectedItem is not OpenChargeRow chosen) return;
            chosenChargeId = chosen.Id;
        }
        else
        {
            item = option.Item;
        }

        using var db = new AppDbContext();
        var plan = PlanAllocations(db, tenantId, Math.Round(typed, 2), chosenChargeId, item);
        if (plan.Allocations == null) return;

        ChangeHint.Text = plan.Change > 0
            ? $"Applies {Fmt.Peso(plan.Applied)}.  Change to return: {Fmt.Peso(plan.Change)}."
            : $"Applies {Fmt.Peso(plan.Applied)}.";
        ChangeHint.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource(
            plan.Change > 0 ? "WarningBrush" : "AppSecondaryBrush");
        ChangeHint.Visibility = Visibility.Visible;
    }

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

        if (PayForCombo.SelectedItem is not PayForOption option)
        {
            Tell("Choose what the tenant is paying for.");
            return;
        }

        int? chosenChargeId = null;
        string? item = null;

        if (option.Specific)
        {
            if (ChargeGrid.SelectedItem is not OpenChargeRow chosen)
            {
                Tell("Click a charge in the Unpaid charges tab first, or choose a different option.");
                Tabs.SelectedIndex = 1;
                return;
            }
            chosenChargeId = chosen.Id;
        }
        else
        {
            item = option.Item;
        }

        // A first look, only to describe the payment in the confirmation.
        int chargeCount;
        decimal plannedApplied;
        decimal plannedChange;
        using (var preview = new AppDbContext())
        {
            var plan = PlanAllocations(preview, tenant.Id, amount, chosenChargeId, item);
            if (plan.Allocations == null)
            {
                Tell(plan.Error!);
                LoadTenantData();
                return;
            }
            chargeCount = plan.Allocations.Count;
            plannedApplied = plan.Applied;
            plannedChange = plan.Change;
        }

        var purpose = option.Specific ? "one specific charge" : item ?? "oldest charges first";
        var confirm = MessageBox.Show(
            $"Record a payment from {tenant.Name}?\n\n" +
            $"Amount received:   {Fmt.Peso(amount)}\n" +
            $"Applied to {chargeCount} charge(s):   {Fmt.Peso(plannedApplied)}\n" +
            (plannedChange > 0 ? $"Change to return:   {Fmt.Peso(plannedChange)}\n" : "") +
            $"\nPaying for: {purpose}",
            "Confirm payment", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        decimal savedApplied = 0;
        decimal savedChange = 0;
        try
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            // Check again inside the transaction, in case the balances changed while the confirmation was open.
            var plan = PlanAllocations(db, tenant.Id, amount, chosenChargeId, item);
            if (plan.Allocations == null || plan.Applied != plannedApplied)
            {
                transaction.Rollback();
                Tell((plan.Error ?? $"The change would now be {Fmt.Peso(plan.Change)} instead of {Fmt.Peso(plannedChange)}.") +
                     "\n\nThe balances changed while you were confirming. Nothing was saved.");
                RefreshAll();
                return;
            }

            db.Payments.Add(new Payment
            {
                TenantId = tenant.Id,
                PaymentDate = DateOnly.FromDateTime(payDate),
                Amount = plan.Applied,        // what counts toward the charges
                AmountReceived = amount,      // what the tenant handed over
                Method = Blank(MethodCombo.Text),
                ReferenceNo = Blank(ReferenceBox.Text),
                Remarks = Blank(RemarksBox.Text),
                Allocations = plan.Allocations
            });
            db.SaveChanges();
            transaction.Commit();   // the payment and its allocations are saved together or not at all

            savedApplied = plan.Applied;
            savedChange = plan.Change;
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The payment could not be saved because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            RefreshAll();
            return;
        }

        AmountBox.Text = ReferenceBox.Text = RemarksBox.Text = "";
        PayForCombo.SelectedIndex = 0;   // back to "oldest charges first"
        Tabs.SelectedIndex = 0;
        RefreshAll();

        if (savedChange > 0)
            Tell($"Payment recorded.\n\nChange to return to {tenant.Name}: {Fmt.Peso(savedChange)}\n" +
                 $"(received {Fmt.Peso(amount)}, applied {Fmt.Peso(savedApplied)})");
    }
}