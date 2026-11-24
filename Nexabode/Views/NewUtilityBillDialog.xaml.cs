using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

// The window for adding a utility bill to the month selected on the Utility Bills page. It holds what the form on
// that page used to hold; the checks and what is saved are unchanged.
public partial class NewUtilityBillDialog : Window
{
    private readonly DateOnly _start;   // first day of the month the bill is listed under

    public NewUtilityBillDialog(DateOnly monthStart, string monthDisplay)
    {
        InitializeComponent();
        _start = monthStart;
        MonthText.Text = $"It will be listed under {monthDisplay}.";

        using (var db = AppDb.Create())
        {
            var scopes = new List<UtilityBillsPage.ScopeOption> { new() { RoomId = null, Display = "Whole house" } };
            scopes.AddRange(db.Rooms.OrderBy(r => r.RoomName).ToList()
                .Select(r => new UtilityBillsPage.ScopeOption { RoomId = r.Id, Display = r.RoomName }));
            ScopeCombo.ItemsSource = scopes;
            ScopeCombo.SelectedIndex = 0;
        }
        UtilityTypeBox.SelectedIndex = 0;
        BillDuePicker.SelectedDate = monthStart.AddDays(19).ToDateTime(TimeOnly.MinValue);   // the 20th

        Loaded += (_, _) => BillAmountBox.Focus();
    }

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    // The two period dates only appear when "A custom period" is chosen.
    private void PeriodMode_Changed(object sender, RoutedEventArgs e)
    {
        if (PeriodPanel == null) return;   // still loading
        PeriodPanel.Visibility = CustomPeriodRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddBill_Click(object sender, RoutedEventArgs e)
    {
        var start = _start;

        var type = UtilityTypeBox.Text.Trim();
        if (type == "")
        {
            Say.Info("Enter the utility type, for example Electricity.");
            return;
        }
        if (!TryMoney(BillAmountBox.Text, out var total) || total <= 0)
        {
            Say.Info("Enter the total bill amount as a number greater than zero.");
            return;
        }
        if (ScopeCombo.SelectedItem is not UtilityBillsPage.ScopeOption scope)
        {
            Say.Info("Choose whether the bill applies to the whole house or one room.");
            return;
        }
        if (BillDuePicker.SelectedDate is not DateTime dueDt)
        {
            Say.Info("Select the due date of each tenant's share.");
            return;
        }

        // Optional billing period: both dates, or neither (then the bill covers the whole month).
        var custom = CustomPeriodRadio.IsChecked == true;
        if (custom && (PeriodFromPicker.SelectedDate == null || PeriodToPicker.SelectedDate == null))
        {
            Say.Info("Select both dates of the billing period, or choose \"The whole month\".");
            return;
        }

        DateOnly? periodStart = null;
        DateOnly? periodEnd = null;
        if (custom)
        {
            var from = DateOnly.FromDateTime(PeriodFromPicker.SelectedDate!.Value);
            var to = DateOnly.FromDateTime(PeriodToPicker.SelectedDate!.Value);
            if (to < from)
            {
                Say.Info("The billing period cannot end before it starts. Select an end date on or after the start date.");
                return;
            }
            if (to.DayNumber - from.DayNumber > 366)
            {
                Say.Info("The billing period cannot be longer than one year. Change the start or end date.");
                return;
            }

            var monthEnd = start.AddMonths(1).AddDays(-1);
            if (to < start || from > monthEnd)
            {
                var ask = ConfirmActionDialog.YesNo(
                    "This billing period does not fall within the selected month, but the bill will still be listed under that month. Continue?",
                    "Billing period", confirmText: "Continue");
                if (ask != MessageBoxResult.Yes) return;
            }

            periodStart = from;
            periodEnd = to;
        }

        var key = BillingHelpers.MonthKey(start);

        using var db = AppDb.Create();
        var lower = type.ToLower();
        var duplicate = db.UtilityBills.Any(b => b.BillingMonth == key
            && b.RoomId == scope.RoomId && b.UtilityType.ToLower() == lower);
        if (duplicate)
        {
            Say.Info("A bill for this utility, month and room (or whole house) already exists. Edit that bill, or change the utility type or the room.");
            return;
        }

        // The same utility and room must not be billed twice for the same days.
        var newPeriod = UtilityRules.EffectivePeriod(key, periodStart, periodEnd)!.Value;
        var overlapping = db.UtilityBills
            .Where(b => b.RoomId == scope.RoomId && b.UtilityType.ToLower() == lower)
            .ToList()
            .Where(b => UtilityRecalculation.PeriodOf(b) is BillPeriod p && UtilityRules.PeriodsOverlap(p, newPeriod))
            .ToList();
        if (overlapping.Count > 0)
        {
            var list = string.Join("\n", overlapping.Select(b =>
                UtilityRecalculation.PeriodOf(b) is BillPeriod p
                    ? $"- {b.BillingMonth}: {Fmt.Date(p.Start)} to {Fmt.Date(p.End)}"
                    : $"- {b.BillingMonth}"));
            var ask = ConfirmActionDialog.YesNo(
                $"A {type} bill for {scope.Display} already covers some of the same days:\n\n{list}\n\n" +
                "Adding this bill may charge tenants twice for those days. Add it anyway?",
                "Overlapping bills", confirmText: "Add anyway");
            if (ask != MessageBoxResult.Yes) return;
        }

        db.UtilityBills.Add(new UtilityBill
        {
            UtilityType = type,
            BillingMonth = key,
            TotalAmount = Math.Round(total, 2),
            RoomId = scope.RoomId,
            DueDate = DateOnly.FromDateTime(dueDt),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        });

        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            Say.Info("The bill could not be saved and nothing was saved. A bill for the same month, utility and room may already exist. Check the list and try again.");
            return;
        }

        DialogResult = true;   // the Utility Bills page reloads its list
    }
}
