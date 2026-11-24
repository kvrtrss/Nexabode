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
        public int TenantId { get; set; }
        public string Tenant { get; set; } = "";
        public string LineTwo => $"{Description}  ·  {BillingMonth}";
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
        public string ReceiptNo { get; set; } = "";
        public string Reference { get; set; } = "";
        public string? Remarks { get; set; }
        public string Details { get; set; } = "";
        public bool IsVoided { get; set; }
        public bool IsArchived { get; set; }
        public string Status => IsVoided ? "Voided" : IsArchived ? "Archived" : "Recorded";
        public string PaidFor { get; set; } = "";
        public decimal Received { get; set; }
        public decimal Change { get; set; }
        public bool FromDeposit { get; set; }   // paid from the tenant's deposit, so voiding it puts the money back in the deposit
        public string? VoidReason { get; set; }
        public List<string> Breakdown { get; set; } = new();   // one entry per charge it paid, e.g. "Rent ₱1,500.00 (Nov 2026)"

        // Second line of the row: what it paid for and how. On a narrow table the date is added in front of it.
        public string LineTwo => string.Join("  ·  ", new[] { PaidFor, Method }.Where(t => !string.IsNullOrWhiteSpace(t)));
        public string LineTwoCompact => string.Join("  ·  ", new[] { DateText, PaidFor, Method }.Where(t => !string.IsNullOrWhiteSpace(t)));

        // The two lines shown under the row when it is selected: what the row itself does not show.
        public string DetailsMain
        {
            get
            {
                var parts = new List<string> { $"Receipt no. {ReceiptNo}", Reference.Length > 0 ? $"Reference no. {Reference}" : "No reference no." };
                if (!string.IsNullOrWhiteSpace(Remarks)) parts.Add($"Remarks: {Remarks!.Trim()}");
                if (IsVoided) parts.Add($"Voided because: {(string.IsNullOrWhiteSpace(VoidReason) ? "no reason given" : VoidReason!.Trim())}");
                if (Change > 0) parts.Add($"Applied {Fmt.Peso(Amount)} of {Fmt.Peso(Received)} received ({Fmt.Peso(Change)} change)");
                return string.Join("     ", parts);
            }
        }
        public string PaidForFull =>
            "Paid for: " + (Breakdown.Count > 0 ? string.Join(",  ", Breakdown) : "no charge (nothing was applied)");
    }

    public class BalanceRow
    {
        public int TenantId { get; set; }
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
        public decimal? Suggested { get; set; }   // the full amount owed for this choice, offered as a one-click fill
    }

    // Set by the Dashboard so that its "Record payment" button opens the window as soon as this page is shown.
    public static bool OpenFormOnLoad { get; set; }

    public PaymentsPage()
    {
        InitializeComponent();

        // Right-click on a row offers the same actions as the buttons.
        GridMenu.Attach(PaymentGrid, EditButton, VoidButton, PrintReceiptButton, ArchiveButton, UnarchiveButton);
        Loaded += (_, _) =>
        {
            _ready = true;
            RefreshAll();

            // Opened from the Dashboard's "Record payment": go straight to the window.
            if (OpenFormOnLoad)
            {
                OpenFormOnLoad = false;
                OpenRecordDialog(null, null);
            }
        };
    }

    // ---------- Recording a payment: it has its own window ----------

    private void OpenForm_Click(object sender, RoutedEventArgs e) => OpenRecordDialog(null, null);

    // 'tenantId' and 'chargeId' are filled in when the window is opened from an unpaid charge.
    private void OpenRecordDialog(int? tenantId, int? chargeId)
    {
        var dialog = new RecordPaymentDialog(tenantId, chargeId);
        if (Owner != null) dialog.Owner = Owner;
        if (dialog.ShowDialog() != true) return;

        Tabs.SelectedIndex = 0;
        RefreshAll();

        // Always shown, so a receipt can be printed right away. It also tells the cashier about any change to return.
        PaymentRecordedDialog.Show(Owner, dialog.SavedPaymentId,
            ReceiptWording.PaymentRecordedMessage(dialog.SavedReceipt, dialog.SavedTenantName, dialog.SavedReceived, dialog.SavedApplied));
    }

    // A narrow table (small screen) drops the Receipt column and puts the date under the tenant's name instead,
    // so the information stays in one look.
    private void PaymentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged) return;
        var compact = PaymentGrid.ActualWidth < 560;
        if ((PaymentGrid.Tag as string == "Compact") == compact) return;

        PaymentGrid.Tag = compact ? "Compact" : null;
        ReceiptColumn.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        GridColumnFix.Refresh(PaymentGrid);
    }

    // ---------- Helpers ----------


    private Window? Owner => Window.GetWindow(this);

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private PaymentRow? SelectedPayment =>
    PaymentGrid.SelectedItems.Count == 1 ? PaymentGrid.SelectedItem as PaymentRow : null;

    private List<PaymentRow> SelectedPayments =>
        PaymentGrid.SelectedItems.OfType<PaymentRow>().ToList();

    private void RefreshAll()
    {
        LoadStats();
        LoadHistory();
        LoadUnpaid();
        LoadAllBalances();
    }

    // ---------- Stat cards ----------

    private void LoadStats()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        using var db = AppDb.Create();

        var payments = db.Payments.ToList().Where(p => !p.IsVoided).ToList();
        var thisMonth = payments.Where(p => p.PaymentDate >= monthStart).ToList();
        var todays = payments.Where(p => p.PaymentDate == today).ToList();

        MonthCard.Value = Fmt.Peso(thisMonth.Sum(p => p.Amount));
        MonthCard.Caption = Fmt.Count(thisMonth.Count, "payment");
        MonthCard.Tone = thisMonth.Count > 0 ? "Success" : "Neutral";

        TodayCard.Value = Fmt.Peso(todays.Sum(p => p.Amount));
        TodayCard.Caption = todays.Count == 0 ? "No payments today" : Fmt.Count(todays.Count, "payment");
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
            ? "No outstanding balances"
            : $"{Fmt.Count(open.Select(x => x.TenantId).Distinct().Count(), "tenant")} with a balance";
        OutstandingCard.Tone = open.Count == 0 ? "Success" : "Warning";

        OverdueCard.Value = Fmt.Peso(overdue.Sum(x => x.Balance));
        OverdueCard.Caption = overdue.Count == 0
            ? "Nothing overdue"
            : $"{Fmt.Count(overdue.Select(x => x.TenantId).Distinct().Count(), "tenant")} overdue";
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

        using var db = AppDb.Create();
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
        using var db = AppDb.Create();
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
                ReceiptNo = p.ReceiptNo ?? "",
                Reference = p.ReferenceNo ?? "",
                Remarks = p.Remarks,
                IsVoided = p.IsVoided,
                IsArchived = p.IsArchived,
                FromDeposit = p.Source == DepositService.PaymentSource,
                PaidFor = string.Join(", ", p.Allocations
                    .Select(a => BillingHelpers.ItemOf(a.Charge))
                    .Distinct()
                    .OrderBy(i => i == "Rent" ? 0 : 1).ThenBy(i => i)),
                Details = p.IsVoided ? $"Voided: {p.VoidReason}" : p.Remarks ?? "",
                VoidReason = p.IsVoided ? p.VoidReason : null,
                Breakdown = p.Allocations
                    .OrderBy(a => a.Charge.DueDate).ThenBy(a => a.ChargeId)
                    .Select(a => $"{BillingHelpers.ItemOf(a.Charge)} {Fmt.Peso(a.AmountApplied)} ({MonthText(a.Charge.BillingMonth)})")
                    .ToList()
            })
            .Where(r => query == ""
                || r.Tenant.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.Method.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.Reference.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.ReceiptNo.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.PaidFor.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)
            .ToList();

        PaymentGrid.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateHistoryButtons();
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void PaymentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHistoryButtons();

    private void UpdateHistoryButtons()
    {
        var selected = SelectedPayment;
        EditButton.IsEnabled = selected is { IsVoided: false };
        VoidButton.IsEnabled = selected is { IsVoided: false };
        PrintReceiptButton.IsEnabled = selected != null && ReceiptWording.CanPrint(selected.IsVoided);

        var rows = SelectedPayments;
        ArchiveButton.IsEnabled = rows.Any(r => !r.IsArchived);
        UnarchiveButton.IsEnabled = rows.Any(r => r.IsArchived);

    }

    private static string MonthText(string billingMonth) =>
        DateOnly.TryParseExact(billingMonth + "-01", "yyyy-MM-dd", out var month) ? month.ToString("MMM yyyy") : billingMonth;

    // Prints the receipt of the selected payment. A reprint is marked as a duplicate copy.
    private void PrintReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPayment is { } row) ReceiptPrinter.Print(Window.GetWindow(this), row.Id);
    }

    private void EditPayment_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedPayment;
        if (row == null || row.IsVoided) return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        var v = FormDialog.Ask(Owner, $"Edit payment: {row.Tenant}", "Save changes",
            new[]
            {
                ("Payment date (e.g. 2026-10-14)", row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("Method", row.Method),
                ("GCash, bank or check ref. no.", row.Reference),
                ("Remarks", row.Remarks ?? "")
            },
            vals =>
            {
                if (!DateOnly.TryParse(vals[0].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                    return "Enter the payment date in the format 2026-10-14.";
                if (date > today) return "The payment date cannot be in the future. Select today's date or an earlier date.";
                return null;
            },
            note: $"Receipt no. {row.ReceiptNo}. Amount: {Fmt.Peso(row.Amount)}. The amount cannot be edited. If it is wrong, void this payment and record a new one.");
        if (v == null) return;

        DateOnly.TryParse(v[0].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var newDate);

        // A short review of what changes. If nothing did, there is nothing to save.
        var reviewList = ConfirmWording.Changes(
            ("Payment date", Fmt.Date(row.Date), Fmt.Date(newDate)),
            ("Method", row.Method, v[1]),
            ("Reference no.", row.Reference, v[2]),
            ("Remarks", row.Remarks, v[3]));
        if (reviewList.Count == 0) return;
        if (!ConfirmActionDialog.Ask(Owner, "Edit payment",
                ConfirmWording.EditRecord($"receipt {row.ReceiptNo} ({row.Tenant})", reviewList), "Save changes")) return;

        using var db = AppDb.Create();
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

        var note = $"This cancels the payment of {Fmt.Peso(row.Amount)} from {row.DateText}. The charges it paid become unpaid again, " +
                   "and the payment remains in the history marked Voided (tick Show voided to see it). This cannot be undone." +
                   (row.Change > 0 ? $"\n\nThe tenant handed over {Fmt.Peso(row.Received)} and {Fmt.Peso(row.Change)} was returned as change." : "");

        // A payment made from the tenant's deposit gives its money back to the deposit, so there is nothing to hand back.
        var fields = new List<(string Label, string Value)> { ("Reason for voiding", "") };
        if (row.FromDeposit)
            note += "\n\nThis payment was made from the tenant's deposit. Voiding it puts the money back into their deposit balance.";
        else
        {
            fields.Add(("Amount given back to the tenant (0 if none)", "0"));
            note += "\n\nIf you gave money back to the tenant, enter how much. The charges it paid will then show as refunded in Billing Charges.";
        }

        var v = FormDialog.Ask(Owner, $"Void payment: {row.Tenant}", "Void payment",
            fields.ToArray(),
            vals =>
            {
                if (!ReasonRules.IsReal(vals[0])) return "Enter a reason for voiding the payment (at least 3 letters or digits).";
                if (vals.Length > 1)
                {
                    if (!decimal.TryParse(vals[1], NumberStyles.Number, CultureInfo.CurrentCulture, out var given) || given < 0)
                        return "Enter the amount given back as a number (use 0 if no money was given back).";
                    if (Math.Round(given, 2) > row.Amount)
                        return $"The amount given back cannot be more than the payment amount ({Fmt.Peso(row.Amount)}). Enter a smaller amount.";
                }
                return null;
            },
            note: note);
        if (v == null) return;

        decimal givenBack = 0;
        if (v.Length > 1 && decimal.TryParse(v[1], NumberStyles.Number, CultureInfo.CurrentCulture, out var typedBack))
            givenBack = Math.Round(typedBack, 2);

        using var db = AppDb.Create();
        var payment = db.Payments
            .Include(p => p.Allocations).ThenInclude(a => a.Charge)
            .First(p => p.Id == row.Id);

        if (payment.IsVoided) { RefreshAll(); return; }

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Remember what the payment had paid, then free those charges.
        var applied = string.Join("; ", payment.Allocations.Select(a =>
            $"{a.Charge.Description ?? a.Charge.ChargeType.ToString()} ({a.Charge.BillingMonth}) {Fmt.Peso(a.AmountApplied)}"));

        // Note the money given back on the charges this payment had paid, oldest due date first.
        var left = givenBack;
        foreach (var allocation in payment.Allocations.OrderBy(a => a.Charge.DueDate).ThenBy(a => a.ChargeId))
        {
            if (left <= 0) break;
            var give = Math.Min(left, allocation.AmountApplied);
            allocation.Charge.RefundedAmount += give;
            allocation.Charge.RefundedOn = today;
            left -= give;
        }

        db.PaymentAllocations.RemoveRange(payment.Allocations);
        payment.IsVoided = true;
        payment.VoidReason = v[0].Trim()
            + (applied == "" ? "" : $" (was applied to: {applied})")
            + (givenBack > 0 ? $" (refunded to tenant: {Fmt.Peso(givenBack)})" : "");
        payment.VoidedOn = today;
        db.SaveChanges();   // everything is saved together or not at all

        RefreshAll();
    }

    // ---------- Unpaid charges (every tenant) ----------

    private void LoadUnpaid()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = AppDb.Create();
        ChargeGrid.ItemsSource = db.Charges
            .Include(c => c.UtilityBill)
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .ToList()
            .Select(c =>
            {
                var paid = c.Allocations.Sum(a => a.AmountApplied);
                return new OpenChargeRow
                {
                    Id = c.Id,
                    TenantId = c.Occupancy.TenantId,
                    Tenant = c.Occupancy.Tenant.FullName,
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
            .OrderBy(r => r.DueDate).ThenBy(r => r.Tenant)
            .ToList();
        UpdateUnpaidButtons();
    }

    private OpenChargeRow? SelectedCharge => ChargeGrid.SelectedItem as OpenChargeRow;

    private void ChargeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateUnpaidButtons();

    private void UpdateUnpaidButtons() => RecordForChargeButton.IsEnabled = SelectedCharge != null;

    // Opens the Record payment window with this tenant and this charge already chosen.
    private void RecordForCharge_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCharge is { } charge) OpenRecordDialog(charge.TenantId, charge.Id);
    }

    private void LoadAllBalances()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = AppDb.Create();
        var charges = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .ToList();

        BalanceGrid.ItemsSource = charges
            .GroupBy(c => c.Occupancy.Tenant)
            .Select(g => new BalanceRow
            {
                TenantId = g.Key.Id,
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

    // Double-click a tenant in All balances to open their account.
    private void BalanceGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element != null; element = System.Windows.Media.VisualTreeHelper.GetParent(element))
        {
            if (element is DataGridRow { Item: BalanceRow row })
            {
                var window = new TenantAccountWindow(row.TenantId);
                if (Owner != null) window.Owner = Owner;
                window.ShowDialog();
                RefreshAll();   // a payment may have been recorded in the window
                return;
            }
            if (element is DataGrid) return;
        }
    }

    // ---------- Record a payment ----------

    // Works out how a payment would be applied, using the balances in the database right now.
    // Charges of one tenant that still have a balance, oldest due date first.
    internal static List<OpenChargeRow> GetOpenCharges(AppDbContext db, int tenantId)
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
    internal static (List<PaymentAllocation>? Allocations, decimal Applied, decimal Change, string? Error) PlanAllocations(
        AppDbContext db, int tenantId, decimal amount, int? chosenChargeId, string? item)
    {
        var open = GetOpenCharges(db, tenantId);
        if (open.Count == 0)
            return (null, 0, 0, "This tenant has no unpaid charges, so there is nothing to apply this payment to.\n\n" +
                                "Nexabode does not accept advance payments, so nothing was recorded.");

        List<OpenChargeRow> scope;
        if (chosenChargeId is int chargeId)
        {
            var fresh = open.FirstOrDefault(r => r.Id == chargeId);
            if (fresh == null) return (null, 0, 0, "That charge has already been fully paid. Select another charge.");
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
}
