using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Services;

namespace Nexabode.Views;

// One tenant's account: what they owe now, what is overdue and what they have paid, in one window.
// It is opened from Billing Charges, Tenants and Payments, so "what does this tenant owe?" has one answer.
public partial class TenantAccountWindow : Window
{
    private readonly int _tenantId;
    private string _tenantName = "";
    private List<BillingPage.ChargeRow> _rows = new();
    private bool _ready;
    // Months the user closed. Everything starts open, and a refresh (Include paid charges, a new payment) keeps this.
    private readonly HashSet<string> _closedMonths = new();

    public class PaymentLine
    {
        public string DateText { get; set; } = "";
        public string ReceiptNo { get; set; } = "";
        public string MethodText { get; set; } = "";
        public decimal Received { get; set; }
    }

    public TenantAccountWindow(int tenantId)
    {
        InitializeComponent();
        _tenantId = tenantId;
        Loaded += (_, _) =>
        {
            _ready = true;
            Load();
        };
    }

    private static void Tone(TextBlock block, string brushKey) =>
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);

    // Reads the tenant's charges and payments again. Called when the window opens and after a payment is recorded.
    private void Load()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = AppDb.Create();
        var tenant = db.Tenants.Find(_tenantId);
        if (tenant == null)
        {
            Close();
            return;
        }

        _tenantName = tenant.FullName;
        Title = $"Tenant account - {tenant.FullName}";
        NameText.Text = tenant.FullName;

        var stay = db.Occupancies
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .Where(o => o.TenantId == _tenantId)
            .ToList()
            .OrderByDescending(o => o.MoveInDate)
            .FirstOrDefault();
        var info = new List<string>();
        if (stay != null) info.Add($"{stay.Bed.Room.RoomName} / {stay.Bed.BedLabel}");
        if (!string.IsNullOrWhiteSpace(tenant.Contact)) info.Add(tenant.Contact!);
        InfoText.Text = info.Count == 0 ? "No stay yet" : string.Join("  ·  ", info);

        // Voided charges are left out automatically.
        _rows = db.Charges
            .Include(c => c.UtilityBill)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Include(c => c.Occupancy).ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .Where(c => c.Occupancy.TenantId == _tenantId)
            .ToList()
            .Select(c => BillingPage.ToRow(c, today))
            .ToList();

        var billed = _rows.Sum(r => r.Amount);
        var paid = _rows.Sum(r => r.Paid);
        var overdue = _rows.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        BilledValue.Text = Fmt.Peso(billed);
        PaidValue.Text = Fmt.Peso(paid);
        BalanceValue.Text = Fmt.Peso(billed - paid);
        OverdueValue.Text = Fmt.Peso(overdue);
        Tone(PaidValue, "SuccessBrush");
        Tone(BalanceValue, billed - paid > 0 ? "WarningBrush" : "SuccessBrush");
        Tone(OverdueValue, overdue > 0 ? "DangerBrush" : "SuccessBrush");

        var payments = db.Payments
            .Where(p => p.TenantId == _tenantId && !p.IsVoided)
            .ToList()
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Select(p => new PaymentLine
            {
                DateText = Fmt.Date(p.PaymentDate),
                ReceiptNo = p.ReceiptNo ?? "",
                MethodText = string.Join("  ·  ", new[] { p.Method, string.IsNullOrWhiteSpace(p.ReferenceNo) ? null : "Ref " + p.ReferenceNo }
                                                   .Where(t => !string.IsNullOrWhiteSpace(t))),
                Received = p.AmountReceived ?? p.Amount
            })
            .ToList();
        PaymentGrid.ItemsSource = payments;
        PaymentGrid.Visibility = payments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        NoPaymentsText.Visibility = payments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ShowCharges();
    }

    // The charges, newest month first, each month under its own heading. Only what is still owed unless asked otherwise.
    private void ShowCharges()
    {
        var includePaid = IncludePaidCheck.IsChecked == true;

        var rows = _rows
            .Where(r => includePaid || r.Balance > 0)
            .OrderByDescending(r => r.Month).ThenBy(r => r.Item == "Rent" ? 0 : 1).ThenBy(r => r.Item).ThenBy(r => r.DueDate)
            .ToList();

        foreach (var month in rows.GroupBy(r => r.Month))
        {
            var left = month.Sum(r => r.Balance);
            var key = new BillingPage.MonthGroupKey
            {
                Month = month.Key,
                Name = month.First().MonthText,
                Summary = Fmt.Count(month.Count(), "charge") + "   ·   " + (left > 0 ? $"Balance {Fmt.Peso(left)}" : "Paid in full")
            };
            key.IsExpanded = !_closedMonths.Contains(month.Key);
            key.ExpandedChanged = (m, open) => { if (open) _closedMonths.Remove(m); else _closedMonths.Add(m); };
            foreach (var row in month) row.MonthGroup = key;
        }

        var view = new ListCollectionView(rows);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BillingPage.ChargeRow.MonthGroup)));
        ChargeGrid.ItemsSource = view;

        EmptyText.Text = _rows.Count == 0
            ? "This tenant has no charges yet."
            : "Nothing is owed. Tick Include paid charges to see everything that was billed.";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void IncludePaid_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) ShowCharges();
    }

    private void RecordPayment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RecordPaymentDialog(_tenantId, null) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        Load();
        PaymentRecordedDialog.Show(this, dialog.SavedPaymentId,
            ReceiptWording.PaymentRecordedMessage(dialog.SavedReceipt, dialog.SavedTenantName, dialog.SavedReceived, dialog.SavedApplied));
    }

    // The same billing statement as in Reports, for every charge and payment of this tenant.
    private void PrintStatement_Click(object sender, RoutedEventArgs e)
    {
        var id = _tenantId;
        PrintPreview.Show(this, $"Billing statement - {_tenantName}", "statement", (width, height) =>
        {
            var doc = ReportsPage.BuildStatement(id, null);   // a fresh copy sized for the paper
            doc.PagePadding = new Thickness(50);
            doc.PageHeight = height;
            doc.PageWidth = width;
            doc.ColumnWidth = width;
            return doc;
        });
    }
}
