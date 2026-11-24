using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

// The window for recording a payment. It has everything the form on the Payments page used to have: the tenant,
// what is being paid for (any item, one item, or one specific charge picked from the list in this window),
// the amount, the method, the date and remarks. The checks and the way a payment is applied are unchanged.
public partial class RecordPaymentDialog : Window
{
    // What was recorded, for the receipt message the Payments page shows next.
    public int SavedPaymentId { get; private set; }
    public string SavedReceipt { get; private set; } = "";
    public string SavedTenantName { get; private set; } = "";
    public decimal SavedReceived { get; private set; }
    public decimal SavedApplied { get; private set; }

    private bool _ready;
    private bool _loadingPayFor;
    private int? _wantedCharge;   // a charge to pick as soon as the tenant's charges are loaded

    // 'tenantId' and 'chargeId' fill in the tenant and the charge when the window is opened from an unpaid charge.
    public RecordPaymentDialog(int? tenantId = null, int? chargeId = null)
    {
        InitializeComponent();
        _wantedCharge = chargeId;

        PayDatePicker.SelectedDate = DateTime.Today;
        MethodCombo.SelectedIndex = 0;

        // The method box can be typed in, so watch its text (picking from the list changes it too).
        MethodCombo.AddHandler(TextBoxBase.TextChangedEvent,
                               new TextChangedEventHandler((_, _) => UpdateReferenceBox()));
        UpdateReferenceBox();

        _ready = true;
        LoadTenants(tenantId);
        RefreshReceiptNumber();

        Loaded += (_, _) => (tenantId == null ? (UIElement)TenantCombo : AmountBox).Focus();
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private int? SelectedTenantId => (TenantCombo.SelectedItem as PaymentsPage.TenantOption)?.Id;

    // ---------- Receipt and reference numbers ----------

    // The receipt number is Nexabode's own: show the one the next payment will get, and do not let anyone type it.
    private void RefreshReceiptNumber()
    {
        var date = DateOnly.FromDateTime(PayDatePicker.SelectedDate ?? DateTime.Today);
        using var db = AppDb.Create();
        ReceiptNoText.Text = $"Receipt no. {ReceiptNumbers.Peek(db, date)} will be assigned when you record the payment.";
    }

    // A reference number comes from outside (GCash, a bank, a check). Cash has none, so the box is switched off.
    private void UpdateReferenceBox()
    {
        var method = MethodCombo.Text?.Trim() ?? "";
        var needsReference = method.Length > 0 && !method.Equals("Cash", StringComparison.OrdinalIgnoreCase);

        if (!needsReference) ReferenceBox.Text = "";
        ReferencePanel.Visibility = needsReference ? Visibility.Visible : Visibility.Collapsed;
    }

    // The receipt number starts again each year, so the payment date can change which number is next.
    private void PayDate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) RefreshReceiptNumber();
    }

    // ---------- Tenant, and what is being paid for ----------

    private void LoadTenants(int? select)
    {
        using var db = AppDb.Create();
        var tenants = db.Tenants.OrderBy(t => t.FullName).ToList()
            .Select(t => new PaymentsPage.TenantOption { Id = t.Id, Name = t.FullName })
            .ToList();

        TenantCombo.ItemsSource = tenants;
        if (select != null)
            TenantCombo.SelectedItem = tenants.FirstOrDefault(t => t.Id == select);
    }

    private void TenantCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        LoadTenantData();
        UpdateChangeHint();
    }

    private void LoadTenantData()
    {
        if (SelectedTenantId is not int tenantId)
        {
            ChargeList.ItemsSource = null;
            _loadingPayFor = true;
            PayForCombo.ItemsSource = null;
            _loadingPayFor = false;
            PayForHint.Text = "";
            ChargePanel.Visibility = Visibility.Collapsed;
            UpdateFillButton();
            SummaryText.Text = "Select a tenant to begin.";
            return;
        }

        using var db = AppDb.Create();

        var open = PaymentsPage.GetOpenCharges(db, tenantId);
        ChargeList.ItemsSource = open;
        LoadPayFor(open);

        var allCharges = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.Occupancy.TenantId == tenantId)
            .ToList();
        var billed = allCharges.Sum(c => c.Amount);
        var paid = allCharges.Sum(c => c.Allocations.Sum(a => a.AmountApplied));
        var overdue = open.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        SummaryText.Text = $"Balance {Fmt.Peso(billed - paid)}  -  Overdue {Fmt.Peso(overdue)}";

        // Opened from an unpaid charge: pick "One specific charge" and that charge.
        if (_wantedCharge is int wanted)
        {
            _wantedCharge = null;
            var charge = open.FirstOrDefault(r => r.Id == wanted);
            if (charge != null && PayForCombo.ItemsSource is List<PaymentsPage.PayForOption> options)
            {
                PayForCombo.SelectedItem = options.FirstOrDefault(o => o.Specific);
                ChargeList.SelectedItem = charge;
            }
        }
    }

    // The "paying for" choices: any item, one item (Rent, Electricity, ...), or one specific charge.
    private void LoadPayFor(List<PaymentsPage.OpenChargeRow> open)
    {
        var keep = (PayForCombo.SelectedItem as PaymentsPage.PayForOption)?.Key;

        var options = new List<PaymentsPage.PayForOption>
        {
            new() { Key = "oldest", Display = "Oldest charges first (any item)",
                    Suggested = open.Sum(r => r.Balance),
                    Hint = "Pays the oldest unpaid charge first, then the next one. Any extra is shown as change to return." }
        };

        foreach (var group in open.GroupBy(r => r.Item)
                                  .OrderBy(g => g.Key == "Rent" ? 0 : 1).ThenBy(g => g.Key))
        {
            var due = group.Sum(r => r.Balance);
            options.Add(new PaymentsPage.PayForOption
            {
                Key = "item:" + group.Key,
                Item = group.Key,
                Suggested = due,
                Display = $"{group.Key}  -  {Fmt.Peso(due)} unpaid",
                Hint = $"Pays only {group.Key} charges, oldest first. Up to {Fmt.Peso(due)}. Any extra is change to return."
            });
        }

        options.Add(new PaymentsPage.PayForOption
        {
            Key = "specific",
            Specific = true,
            Display = "One specific charge (choose it from the list)",
            Hint = "Choose the charge in the list below, then enter the amount."
        });

        _loadingPayFor = true;
        PayForCombo.ItemsSource = options;
        PayForCombo.SelectedItem = options.FirstOrDefault(o => o.Key == keep) ?? options[0];
        _loadingPayFor = false;

        var selected = PayForCombo.SelectedItem as PaymentsPage.PayForOption;
        PayForHint.Text = selected?.Hint ?? "";
        ChargePanel.Visibility = selected is { Specific: true } ? Visibility.Visible : Visibility.Collapsed;
        UpdateFillButton();
    }

    private void PayFor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _loadingPayFor) return;

        if (PayForCombo.SelectedItem is not PaymentsPage.PayForOption option)
        {
            PayForHint.Text = "";
            ChargePanel.Visibility = Visibility.Collapsed;
            return;
        }

        PayForHint.Text = option.Hint;
        ChargePanel.Visibility = option.Specific ? Visibility.Visible : Visibility.Collapsed;
        UpdateFillButton();
        UpdateChangeHint();
    }

    private void ChargeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFillButton();
        UpdateChangeHint();
    }

    // The full amount owed for what is selected (all of it, one item, or the one chosen charge), if there is one.
    private decimal? SuggestedAmount()
    {
        if (PayForCombo.SelectedItem is not PaymentsPage.PayForOption option) return null;
        var amount = option.Specific ? (ChargeList.SelectedItem as PaymentsPage.OpenChargeRow)?.Balance : option.Suggested;
        return amount > 0 ? amount : null;
    }

    // Offers the full amount owed as a one-click fill. The amount box is never filled in on its own,
    // because "amount received" is a fact about the money handed over, and a wrong guess would be recorded as real.
    private void UpdateFillButton()
    {
        if (SuggestedAmount() is decimal amount)
        {
            FillBalanceButton.Content = $"Use {Fmt.Peso(amount)}";
            FillBalanceButton.Visibility = Visibility.Visible;
        }
        else
        {
            FillBalanceButton.Visibility = Visibility.Collapsed;
        }
    }

    private void FillBalance_Click(object sender, RoutedEventArgs e)
    {
        if (SuggestedAmount() is not decimal amount) return;
        AmountBox.Text = amount.ToString("0.00", CultureInfo.CurrentCulture);
        AmountBox.Focus();
        AmountBox.CaretIndex = AmountBox.Text.Length;
    }

    // ---------- Amount ----------

    private void AmountBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateChangeHint();

    // Shows, while typing, how much of the amount will be applied and how much is change to return.
    private void UpdateChangeHint()
    {
        if (!_ready) return;
        ChangeHint.Visibility = Visibility.Collapsed;

        if (SelectedTenantId is not int tenantId) return;
        if (PayForCombo.SelectedItem is not PaymentsPage.PayForOption option) return;
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var typed) || typed <= 0)
            return;

        int? chosenChargeId = null;
        string? item = null;
        if (option.Specific)
        {
            if (ChargeList.SelectedItem is not PaymentsPage.OpenChargeRow chosen) return;
            chosenChargeId = chosen.Id;
        }
        else
        {
            item = option.Item;
        }

        using var db = AppDb.Create();
        var plan = PaymentsPage.PlanAllocations(db, tenantId, Math.Round(typed, 2), chosenChargeId, item);
        if (plan.Allocations == null) return;

        ChangeHint.Text = plan.Change > 0
            ? $"Applies {Fmt.Peso(plan.Applied)}.  Change to return: {Fmt.Peso(plan.Change)}."
            : $"Applies {Fmt.Peso(plan.Applied)}.";
        ChangeHint.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource(
            plan.Change > 0 ? "WarningBrush" : "AppSecondaryBrush");
        ChangeHint.Visibility = Visibility.Visible;
    }

    // ---------- Recording ----------

    private void RecordPayment_Click(object sender, RoutedEventArgs e)
    {
        if (TenantCombo.SelectedItem is not PaymentsPage.TenantOption tenant)
        {
            Say.Info("Select a tenant first.");
            return;
        }
        if (PayDatePicker.SelectedDate is not DateTime payDate)
        {
            Say.Info("Select the payment date.");
            return;
        }
        if (DateOnly.FromDateTime(payDate) > DateOnly.FromDateTime(DateTime.Today))
        {
            Say.Info("The payment date cannot be in the future. Select today's date or an earlier date.");
            return;
        }
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            Say.Info("Enter the payment amount as a number greater than zero.");
            return;
        }
        amount = Math.Round(amount, 2);

        if (PayForCombo.SelectedItem is not PaymentsPage.PayForOption option)
        {
            Say.Info("Choose what the tenant is paying for.");
            return;
        }

        int? chosenChargeId = null;
        string? item = null;

        if (option.Specific)
        {
            if (ChargeList.SelectedItem is not PaymentsPage.OpenChargeRow chosen)
            {
                Say.Info("Choose a charge from the list first, or choose a different option.");
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
        using (var preview = AppDb.Create())
        {
            var plan = PaymentsPage.PlanAllocations(preview, tenant.Id, amount, chosenChargeId, item);
            if (plan.Allocations == null)
            {
                Say.Info(plan.Error!);
                LoadTenantData();
                return;
            }
            chargeCount = plan.Allocations.Count;
            plannedApplied = plan.Applied;
            plannedChange = plan.Change;
        }

        var purpose = option.Specific ? "one specific charge" : item ?? "oldest charges first";
        var confirm = ConfirmActionDialog.YesNo(
            $"Record a payment from {tenant.Name}?\n\n" +
            $"Amount received:   {Fmt.Peso(amount)}\n" +
            $"Applied to {Fmt.Count(chargeCount, "charge")}:   {Fmt.Peso(plannedApplied)}\n" +
            (plannedChange > 0 ? $"Change to return:   {Fmt.Peso(plannedChange)}\n" : "") +
            $"\nPaying for: {purpose}\n\nA recorded payment can be voided later if it was entered by mistake.",
            "Confirm payment", confirmText: "Record payment");
        if (confirm != MessageBoxResult.Yes) return;

        decimal savedApplied;
        string savedReceipt;
        int savedPaymentId;
        try
        {
            using var db = AppDb.Create();
            using var transaction = db.Database.BeginTransaction();

            // Check again inside the transaction, in case the balances changed while the confirmation was open.
            var plan = PaymentsPage.PlanAllocations(db, tenant.Id, amount, chosenChargeId, item);
            if (plan.Allocations == null || plan.Applied != plannedApplied)
            {
                transaction.Rollback();
                Say.Info((plan.Error ?? $"The change to return would now be {Fmt.Peso(plan.Change)} instead of {Fmt.Peso(plannedChange)}.") +
                     "\n\nThe balances changed while you were confirming this payment. Nothing was saved. Review the updated amounts and record the payment again.");
                LoadTenantData();
                return;
            }

            var newPayment = new Payment
            {
                TenantId = tenant.Id,
                PaymentDate = DateOnly.FromDateTime(payDate),
                Amount = plan.Applied,        // what counts toward the charges
                AmountReceived = amount,      // what the tenant handed over
                Method = Blank(MethodCombo.Text),
                ReferenceNo = Blank(ReferenceBox.Text),
                Remarks = Blank(RemarksBox.Text),
                Allocations = plan.Allocations
            };
            db.Payments.Add(newPayment);
            db.SaveChanges();       // also gives the payment its receipt number
            transaction.Commit();   // the payment and its allocations are saved together or not at all

            savedReceipt = newPayment.ReceiptNo ?? "";
            savedPaymentId = newPayment.Id;
            savedApplied = plan.Applied;
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Say.Info("The payment could not be saved because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            LoadTenantData();
            return;
        }

        SavedPaymentId = savedPaymentId;
        SavedReceipt = savedReceipt;
        SavedTenantName = tenant.Name;
        SavedReceived = amount;
        SavedApplied = savedApplied;
        DialogResult = true;   // the Payments page shows the receipt message next
    }
}
