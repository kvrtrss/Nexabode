using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

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
        public string Type { get; set; } = "";
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
        public string DateText { get; set; } = "";
        public decimal Amount { get; set; }
        public string Method { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Remarks { get; set; } = "";
    }

    public class BalanceRow
    {
        public string Tenant { get; set; } = "";
        public decimal Billed { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public decimal Overdue { get; set; }
    }

    public PaymentsPage()
    {
        InitializeComponent();
        PayDatePicker.SelectedDate = DateTime.Today;
        MethodCombo.SelectedIndex = 0;
        Loaded += (_, _) => { LoadTenants(); LoadAllBalances(); };
    }

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private int? SelectedTenantId => (TenantCombo.SelectedItem as TenantOption)?.Id;

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

    private void TenantCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadTenantData();

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
                var balance = c.Amount - paid;
                return new OpenChargeRow
                {
                    Id = c.Id,
                    BillingMonth = c.BillingMonth,
                    Type = c.ChargeType.ToString(),
                    Description = c.Description ?? "",
                    Amount = c.Amount,
                    Paid = paid,
                    Balance = balance,
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
            PaymentGrid.ItemsSource = null;
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

        SummaryText.Text = $"Billed: {billed:N2}     Paid: {paid:N2}     " +
                           $"Balance: {billed - paid:N2}     Overdue: {overdue:N2}";

        PaymentGrid.ItemsSource = db.Payments
            .Where(p => p.TenantId == tenantId)
            .ToList()
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Select(p => new PaymentRow
            {
                DateText = p.PaymentDate.ToString("MMM d, yyyy"),
                Amount = p.Amount,
                Method = p.Method ?? "",
                Reference = p.ReferenceNo ?? "",
                Remarks = p.Remarks ?? ""
            })
            .ToList();
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
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
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
            Tell($"The amount ({amount:N2}) is more than the tenant's balance ({totalBalance:N2}).\n" +
                 "Advance payments are not supported yet.");
            return;
        }

        var allocations = new List<PaymentAllocation>();

        if (SelectedRadio.IsChecked == true)
        {
            if (ChargeGrid.SelectedItem is not OpenChargeRow chosen)
            {
                Tell("Click a charge in the Unpaid charges list first, or choose \"oldest charges first\".");
                return;
            }
            // Use the fresh balance from the database, not the grid.
            var fresh = open.FirstOrDefault(r => r.Id == chosen.Id);
            if (fresh == null)
            {
                Tell("That charge is already fully paid.");
                LoadTenantData();
                return;
            }
            if (amount > fresh.Balance)
            {
                Tell($"That charge only has {fresh.Balance:N2} left. Lower the amount, " +
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
            $"Record a payment of {amount:N2} from {tenant.Name}?\n\nIt will be applied to {allocations.Count} charge(s).",
            "Confirm payment", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        db.Payments.Add(new Payment
        {
            TenantId = tenant.Id,
            PaymentDate = DateOnly.FromDateTime(payDate),
            Amount = amount,
            Method = NullIfBlank(MethodCombo.Text),
            ReferenceNo = NullIfBlank(ReferenceBox.Text),
            Remarks = NullIfBlank(RemarksBox.Text),
            Allocations = allocations
        });
        db.SaveChanges();   // payment and its allocations are saved together or not at all

        AmountBox.Text = ReferenceBox.Text = RemarksBox.Text = "";
        OldestRadio.IsChecked = true;
        LoadTenantData();
        LoadAllBalances();
    }
}