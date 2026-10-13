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
        public string DueText { get; set; } = "";
        public bool IsSplit { get; set; }
        public string StatusText => IsSplit ? "Split" : "Draft";
    }

    public UtilityBillsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

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
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedMonth is not DateOnly start) return;
        BillDuePicker.SelectedDate = start.AddDays(19).ToDateTime(TimeOnly.MinValue);   // the 20th
        LoadBills();
    }

    private void LoadBills()
    {
        if (SelectedMonth is not DateOnly start) return;
        var key = BillingHelpers.MonthKey(start);

        using var db = new AppDbContext();
        var splitIds = db.Charges
            .Where(c => c.UtilityBillId != null)
            .Select(c => c.UtilityBillId!.Value)
            .Distinct()
            .ToList();

        var bills = db.UtilityBills
            .Include(b => b.Room)
            .Where(b => b.BillingMonth == key)
            .ToList();

        BillGrid.ItemsSource = bills
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

        decimal Sum(Func<UtilityBill, bool> match) => bills.Where(match).Sum(b => b.TotalAmount);
        bool Is(UtilityBill b, string name) => b.UtilityType.Trim().Equals(name, StringComparison.OrdinalIgnoreCase);

        var electricity = Sum(b => Is(b, "Electricity"));
        var water = Sum(b => Is(b, "Water"));
        var total = bills.Sum(b => b.TotalAmount);

        ElectricityCard.Value = Fmt.Peso(electricity);
        ElectricityCard.Caption = $"{Start(start)} bill";
        WaterCard.Value = Fmt.Peso(water);
        WaterCard.Caption = $"{Start(start)} bill";
        OtherCard.Value = Fmt.Peso(total - electricity - water);
        OtherCard.Caption = "Internet and others";
        TotalCard.Value = Fmt.Peso(total);
        TotalCard.Caption = $"{bills.Count} bill(s) this month";
        TotalCard.Tone = "Info";
    }

    private static string Start(DateOnly month) => month.ToString("MMMM");

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

        var key = BillingHelpers.MonthKey(start);

        using var db = new AppDbContext();
        var duplicate = db.UtilityBills.Any(b => b.BillingMonth == key
            && b.RoomId == scope.RoomId && b.UtilityType.ToLower() == type.ToLower());
        if (duplicate)
        {
            Tell("A bill for that utility, month, and room/house already exists.");
            return;
        }

        db.UtilityBills.Add(new UtilityBill
        {
            UtilityType = type,
            BillingMonth = key,
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
            $"Split {Fmt.Peso(bill.TotalAmount)} ({bill.UtilityType}) among {stays.Count} tenant(s)?\n\n" +
            $"Each pays about {Fmt.Peso(share)}.\nThis can't be undone.",
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

        var confirm = MessageBox.Show($"Delete the {row.UtilityType} bill of {Fmt.Peso(row.Total)}?",
            "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        using var db = new AppDbContext();
        db.UtilityBills.Remove(db.UtilityBills.Find(row.Id)!);
        db.SaveChanges();
        LoadBills();
    }
}