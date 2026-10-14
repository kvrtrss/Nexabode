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
        public string Tenant { get; set; } = "";
        public string Bed { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public string DueText { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public BillingPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

    private void Setup()
    {
        var months = BillingHelpers.BuildMonths();
        MonthCombo.ItemsSource = months;
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedMonth is not DateOnly start) return;
        RentDuePicker.SelectedDate = start.AddDays(4).ToDateTime(TimeOnly.MinValue);   // the 5th
        LoadCharges();
    }

    private void LoadCharges()
    {
        if (SelectedMonth is not DateOnly start) return;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var key = BillingHelpers.MonthKey(start);

        using var db = new AppDbContext();
        var charges = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Include(c => c.Occupancy).ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .Where(c => c.BillingMonth == key)
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
                    Description = c.Description ?? c.ChargeType.ToString(),
                    Amount = c.Amount,
                    Paid = paid,
                    Balance = balance,
                    DueText = c.DueDate.ToString("MMM d, yyyy"),
                    Status = balance <= 0 ? "Paid"
                           : c.DueDate < today ? "Overdue"
                           : paid > 0 ? "Partial" : "Unpaid"
                };
            })
            .OrderBy(r => r.Tenant).ThenBy(r => r.Description)
            .ToList();

        ChargeGrid.ItemsSource = rows;

        var billed = rows.Sum(r => r.Amount);
        var paidTotal = rows.Sum(r => r.Paid);
        var overdue = rows.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        BilledCard.Value = Fmt.Peso(billed);
        BilledCard.Caption = $"{rows.Count} charge(s)";

        PaidCard.Value = Fmt.Peso(paidTotal);
        PaidCard.Caption = billed == 0 ? "Nothing billed yet" : $"{paidTotal * 100 / billed:0}% collected";
        PaidCard.Tone = "Success";

        BalanceCard.Value = Fmt.Peso(billed - paidTotal);
        BalanceCard.Caption = "Still to collect";
        BalanceCard.Tone = "Warning";

        OverdueCard.Value = Fmt.Peso(overdue);
        OverdueCard.Caption = overdue > 0 ? "Past due date" : "Nothing overdue";
        OverdueCard.Tone = overdue > 0 ? "Danger" : "Success";
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

        var key = BillingHelpers.MonthKey(start);
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
            var alreadyBilled = db.Charges.Any(c => c.OccupancyId == stay.Id
                && c.BillingMonth == key && c.ChargeType == ChargeType.Rent);
            if (alreadyBilled) { skipped++; continue; }

            var amount = stay.AgreedRent;
            var description = "Monthly rent";

            if (prorate || multiStayTenants.Contains(stay.TenantId))
            {
                var from = stay.MoveInDate > start ? stay.MoveInDate : start;
                var to = stay.MoveOutDate is DateOnly mo && mo < end ? mo : end;

                // If the tenant moved straight into another bed on the day this stay ended,
                // that day belongs to the new stay, so don't count it twice.
                if (stay.MoveOutDate is DateOnly outDay &&
                    stays.Any(s => s.TenantId == stay.TenantId && s.Id != stay.Id && s.MoveInDate == outDay))
                {
                    to = to.AddDays(-1);
                }

                var days = to.DayNumber - from.DayNumber + 1;
                if (days <= 0) continue;

                if (days < daysInMonth)
                {
                    amount = Math.Round(stay.AgreedRent * days / daysInMonth, 2);
                    description = $"Rent ({days} of {daysInMonth} days)";
                    if (multiStayTenants.Contains(stay.TenantId)) byDays++;
                }
            }

            db.Charges.Add(new Charge
            {
                OccupancyId = stay.Id,
                BillingMonth = key,
                ChargeType = ChargeType.Rent,
                Description = description,
                Amount = amount,
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