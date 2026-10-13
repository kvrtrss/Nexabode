using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Views;

public partial class BillingPage : Page
{
    public class MonthOption
    {
        public DateOnly Start { get; set; }
        public string Display => Start.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }

    public class ScopeOption
    {
        public int? RoomId { get; set; }
        public string Display { get; set; } = "";
    }

    public class ChargeRow
    {
        public string Tenant { get; set; } = "";
        public string Bed { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public string DueText { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class BillRow
    {
        public int Id { get; set; }
        public string UtilityType { get; set; } = "";
        public string Scope { get; set; } = "";
        public decimal Total { get; set; }
        public string DueText { get; set; } = "";
        public bool IsSplit { get; set; }
        public string SplitText => IsSplit ? "Yes" : "Not yet";
    }

    public BillingPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private static string Key(DateOnly monthStart) =>
        monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

    private void Setup()
    {
        // Months: 6 back to 1 ahead of today.
        var thisMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var months = Enumerable.Range(-6, 8)
            .Select(i => new MonthOption { Start = thisMonth.AddMonths(i) })
            .Reverse()
            .ToList();
        MonthCombo.ItemsSource = months;

        using var db = new AppDbContext();
        var scopes = new List<ScopeOption> { new() { RoomId = null, Display = "Whole house" } };
        scopes.AddRange(db.Rooms.OrderBy(r => r.RoomName).ToList()
            .Select(r => new ScopeOption { RoomId = r.Id, Display = r.RoomName }));
        ScopeCombo.ItemsSource = scopes;
        ScopeCombo.SelectedIndex = 0;
        UtilityTypeBox.SelectedIndex = 0;

        MonthCombo.SelectedItem = months.First(m => m.Start == thisMonth);
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedMonth is not DateOnly start) return;

        RentDuePicker.SelectedDate = start.AddDays(4).ToDateTime(TimeOnly.MinValue);   // the 5th
        BillDuePicker.SelectedDate = start.AddDays(19).ToDateTime(TimeOnly.MinValue);  // the 20th
        LoadCharges();
        LoadBills();
    }

    // ---------- Charges ----------

    private void LoadCharges()
    {
        if (SelectedMonth is not DateOnly start) return;
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();
        var charges = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Include(c => c.Occupancy).ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .Where(c => c.BillingMonth == Key(start))
            .ToList();

        var rows = charges
            .Select(c =>
            {
                var paid = c.Allocations.Sum(a => a.AmountApplied);
                var balance = c.Amount - paid;
                return new ChargeRow
                {
                    Tenant = c.Occupancy.Tenant.FullName,
                    Bed = $"{c.Occupancy.Bed.Room.RoomName} / {c.Occupancy.Bed.BedLabel}",
                    Type = c.ChargeType.ToString(),
                    Description = c.Description ?? "",
                    Amount = c.Amount,
                    Paid = paid,
                    Balance = balance,
                    DueText = c.DueDate.ToString("MMM d, yyyy"),
                    Status = balance <= 0 ? "Paid"
                           : c.DueDate < today ? "Overdue"
                           : paid > 0 ? "Partial" : "Unpaid"
                };
            })
            .OrderBy(r => r.Tenant).ThenBy(r => r.Type)
            .ToList();

        ChargeGrid.ItemsSource = rows;
        TotalsText.Text = $"Billed: {rows.Sum(r => r.Amount):N2}     " +
                          $"Paid: {rows.Sum(r => r.Paid):N2}     " +
                          $"Balance: {rows.Sum(r => r.Balance):N2}";
    }

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

        var end = start.AddMonths(1).AddDays(-1);
        var daysInMonth = end.Day;
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

        var created = 0;
        var skipped = 0;

        foreach (var stay in stays)
        {
            var alreadyBilled = db.Charges.Any(c => c.OccupancyId == stay.Id
                && c.BillingMonth == Key(start) && c.ChargeType == ChargeType.Rent);
            if (alreadyBilled) { skipped++; continue; }

            var amount = stay.AgreedRent;
            var description = "Monthly rent";

            if (prorate)
            {
                var from = stay.MoveInDate > start ? stay.MoveInDate : start;
                var to = stay.MoveOutDate is DateOnly mo && mo < end ? mo : end;
                var days = to.DayNumber - from.DayNumber + 1;
                if (days < daysInMonth)
                {
                    amount = Math.Round(stay.AgreedRent * days / daysInMonth, 2);
                    description = $"Rent ({days} of {daysInMonth} days)";
                }
            }

            db.Charges.Add(new Charge
            {
                OccupancyId = stay.Id,
                BillingMonth = Key(start),
                ChargeType = ChargeType.Rent,
                Description = description,
                Amount = amount,
                DueDate = due
            });
            created++;
        }

        db.SaveChanges();
        LoadCharges();
        Tell($"Rent charges created: {created}.\nAlready billed (skipped): {skipped}.");
    }

    // ---------- Utility bills ----------

    private void LoadBills()
    {
        if (SelectedMonth is not DateOnly start) return;

        using var db = new AppDbContext();
        var splitIds = db.Charges
            .Where(c => c.UtilityBillId != null)
            .Select(c => c.UtilityBillId!.Value)
            .Distinct()
            .ToList();

        BillGrid.ItemsSource = db.UtilityBills
            .Include(b => b.Room)
            .Where(b => b.BillingMonth == Key(start))
            .ToList()
            .OrderBy(b => b.UtilityType)
            .Select(b => new BillRow
            {
                Id = b.Id,
                UtilityType = b.UtilityType,
                Scope = b.Room?.RoomName ?? "Whole house",
                Total = b.TotalAmount,
                DueText = b.DueDate?.ToString("MMM d, yyyy") ?? "-",
                IsSplit = splitIds.Contains(b.Id)
            })
            .ToList();
    }

    private void AddBill_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMonth is not DateOnly start)
        {
            Tell("Select a billing month first.");
            return;
        }

        var type = UtilityTypeBox.Text.Trim();
        if (type == "")
        {
            Tell("Enter the utility type (e.g. Electricity).");
            return;
        }
        if (!decimal.TryParse(BillAmountBox.Text, out var total) || total <= 0)
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

        using var db = new AppDbContext();
        var duplicate = db.UtilityBills.Any(b => b.BillingMonth == Key(start)
            && b.RoomId == scope.RoomId && b.UtilityType.ToLower() == type.ToLower());
        if (duplicate)
        {
            Tell("A bill for that utility, month, and room/house already exists.");
            return;
        }

        db.UtilityBills.Add(new UtilityBill
        {
            UtilityType = type,
            BillingMonth = Key(start),
            TotalAmount = total,
            RoomId = scope.RoomId,
            DueDate = DateOnly.FromDateTime(dueDt)
        });
        db.SaveChanges();

        BillAmountBox.Text = "";
        LoadBills();
    }

    private void SplitBill_Click(object sender, RoutedEventArgs e)
    {
        if (BillGrid.SelectedItem is not BillRow row)
        {
            Tell("Select a bill in the list first.");
            return;
        }
        if (row.IsSplit)
        {
            Tell("This bill has already been split.");
            return;
        }
        if (SelectedMonth is not DateOnly start) return;

        var end = start.AddMonths(1).AddDays(-1);

        using var db = new AppDbContext();
        var bill = db.UtilityBills.Find(row.Id)!;

        var stays = db.Occupancies
            .Include(o => o.Bed)
            .Where(o => o.MoveInDate <= end && (o.MoveOutDate == null || o.MoveOutDate >= start))
            .ToList()
            .Where(o => bill.RoomId == null || o.Bed.RoomId == bill.RoomId)
            .OrderBy(o => o.Id)
            .ToList();

        if (stays.Count == 0)
        {
            Tell("No tenants were staying there during this month, so the bill can't be split.");
            return;
        }

        var share = Math.Round(bill.TotalAmount / stays.Count, 2, MidpointRounding.ToZero);
        var leftover = bill.TotalAmount - share * stays.Count;

        var confirm = MessageBox.Show(
            $"Split {bill.TotalAmount:N2} ({bill.UtilityType}) among {stays.Count} tenant(s)?\n\n" +
            $"Each pays about {share:N2}.\nThis can't be undone.",
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
                Amount = i == 0 ? share + leftover : share,   // leftover centavos go to the first tenant
                DueDate = bill.DueDate ?? end,
                UtilityBillId = bill.Id
            });
        }
        db.SaveChanges();

        LoadBills();
        LoadCharges();
    }

    private void DeleteBill_Click(object sender, RoutedEventArgs e)
    {
        if (BillGrid.SelectedItem is not BillRow row)
        {
            Tell("Select a bill in the list first.");
            return;
        }
        if (row.IsSplit)
        {
            Tell("This bill has already been split into charges, so it can't be deleted.");
            return;
        }

        var confirm = MessageBox.Show($"Delete the {row.UtilityType} bill of {row.Total:N2}?",
            "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        using var db = new AppDbContext();
        db.UtilityBills.Remove(db.UtilityBills.Find(row.Id)!);
        db.SaveChanges();
        LoadBills();
    }
}