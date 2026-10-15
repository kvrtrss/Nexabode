using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class RentBillingPage : Page
{
    public class RentRow
    {
        public int OccupancyId { get; set; }
        public string Tenant { get; set; } = "";
        public string Bed { get; set; } = "";
        public string Details { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public bool Generated { get; set; }
        public bool IsVoided { get; set; }
        public DateOnly? DueDate { get; set; }
        public decimal Balance => Amount - Paid;
        public string Status => IsVoided ? "Voided" : Generated ? "Generated" : "Not generated";
    }

    private List<RentRow> _rows = new();
    private bool _ready;

    public RentBillingPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    // ---------- Helpers ----------

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

    private void Setup()
    {
        var months = BillingHelpers.BuildMonths();
        MonthCombo.ItemsSource = months;
        _ready = true;
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || SelectedMonth is not DateOnly start) return;
        RentDuePicker.SelectedDate = start.AddDays(4).ToDateTime(TimeOnly.MinValue);   // the 5th
        LoadList();
    }

    private void Prorate_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) LoadList();
    }

    private void RentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons() =>
        GenerateSelectedButton.IsEnabled = RentGrid.SelectedItem is RentRow { Generated: false, IsVoided: false };

    // ---------- The list: what rent exists and what is still to be generated ----------

    private static List<RentRow> BuildRows(AppDbContext db, DateOnly start, bool prorate)
    {
        var end = start.AddMonths(1).AddDays(-1);
        var key = BillingHelpers.MonthKey(start);

        var stays = db.Occupancies
            .Include(o => o.Tenant)
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .Where(o => o.MoveInDate <= end && (o.MoveOutDate == null || o.MoveOutDate >= start))
            .ToList();

        // A tenant with more than one stay in the month (for example, a bed change) is billed
        // by days for each stay, so they never pay the full rent twice.
        var multiStayTenants = stays
            .GroupBy(s => s.TenantId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        // Voided rent charges are included on purpose: they count as already billed.
        var existing = db.Charges.IgnoreQueryFilters()
            .Include(c => c.Allocations)
            .Where(c => c.BillingMonth == key && c.ChargeType == ChargeType.Rent)
            .ToList()
            .GroupBy(c => c.OccupancyId)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<RentRow>();

        foreach (var stay in stays.OrderBy(s => s.Tenant.FullName).ThenBy(s => s.MoveInDate))
        {
            var bed = $"{stay.Bed.Room.RoomName} / {stay.Bed.BedLabel}";

            if (existing.TryGetValue(stay.Id, out var charge))
            {
                rows.Add(new RentRow
                {
                    OccupancyId = stay.Id,
                    Tenant = stay.Tenant.FullName,
                    Bed = bed,
                    Details = charge.Description ?? "Monthly rent",
                    Amount = charge.Amount,
                    Paid = charge.Allocations.Sum(a => a.AmountApplied),
                    Generated = true,
                    IsVoided = charge.IsVoided,
                    DueDate = charge.DueDate
                });
                continue;
            }

            var byDays = prorate || multiStayTenants.Contains(stay.TenantId);
            var handover = stay.MoveOutDate is DateOnly outDay &&
                stays.Any(s => s.TenantId == stay.TenantId && s.Id != stay.Id && s.MoveInDate == outDay);

            var rent = BillingRules.CalculateRent(stay.AgreedRent, stay.MoveInDate, stay.MoveOutDate,
                                                  start, byDays, handover);
            if (rent == null) continue;   // no billable days this month

            rows.Add(new RentRow
            {
                OccupancyId = stay.Id,
                Tenant = stay.Tenant.FullName,
                Bed = bed,
                Details = rent.Description,
                Amount = rent.Amount
            });
        }

        return rows;
    }

    private void LoadList()
    {
        if (SelectedMonth is not DateOnly start) return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();
        _rows = BuildRows(db, start, ProrateCheck.IsChecked == true);
        RentGrid.ItemsSource = _rows;

        var live = _rows.Where(r => !r.IsVoided).ToList();
        var generated = live.Where(r => r.Generated).ToList();
        var missing = live.Count - generated.Count;
        var unpaid = generated.Sum(r => Math.Max(0, r.Balance));
        var overdue = generated
            .Where(r => r.DueDate is DateOnly due && due < today)
            .Sum(r => Math.Max(0, r.Balance));

        TotalCard.Value = Fmt.Peso(live.Sum(r => r.Amount));
        TotalCard.Caption = "Expected this month";
        TotalCard.Tone = "Neutral";

        GeneratedCard.Value = generated.Count.ToString();
        GeneratedCard.Caption = live.Count == 0 ? "No stays this month"
                              : missing == 0 ? "All rent generated"
                              : $"{missing} not generated yet";
        GeneratedCard.Tone = live.Count == 0 ? "Neutral" : missing > 0 ? "Warning" : "Success";

        UnpaidCard.Value = Fmt.Peso(unpaid);
        UnpaidCard.Caption = "Outstanding rent";
        UnpaidCard.Tone = unpaid > 0 ? "Warning" : "Success";

        OverdueCard.Value = Fmt.Peso(overdue);
        OverdueCard.Caption = overdue > 0 ? "Past due date" : "Nothing overdue";
        OverdueCard.Tone = overdue > 0 ? "Danger" : "Success";

        UpdateButtons();
    }

    // ---------- Generate ----------

    private void GenerateAll_Click(object sender, RoutedEventArgs e)
    {
        var todo = _rows.Where(r => !r.Generated && !r.IsVoided).ToList();

        if (todo.Count == 0)
        {
            Tell(_rows.Count == 0
                ? "No tenants were staying in that month, so there is nothing to bill."
                : "All rent for this month has already been generated.");
            return;
        }

        Generate(todo);
    }

    private void GenerateSelected_Click(object sender, RoutedEventArgs e)
    {
        if (RentGrid.SelectedItem is not RentRow { Generated: false, IsVoided: false } row)
        {
            Tell("Select a tenant whose rent is not generated yet.");
            return;
        }

        Generate(new List<RentRow> { row });
    }

    private void Generate(List<RentRow> todo)
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

        var due = DateOnly.FromDateTime(dueDt);
        var monthName = (MonthCombo.SelectedItem as MonthOption)?.Display ?? "this month";

        var confirm = MessageBox.Show(
            $"Create {todo.Count} rent charge(s) for {monthName}, totalling {Fmt.Peso(todo.Sum(r => r.Amount))}, " +
            $"due {Fmt.Date(due)}?",
            "Confirm rent", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var count = todo.Count;
        var partial = todo.Count(r => r.Details.StartsWith("Rent (", StringComparison.Ordinal));
        var key = BillingHelpers.MonthKey(start);

        using (var db = new AppDbContext())
        {
            foreach (var row in todo)
            {
                db.Charges.Add(new Charge
                {
                    OccupancyId = row.OccupancyId,
                    BillingMonth = key,
                    ChargeType = ChargeType.Rent,
                    Description = row.Details,
                    Amount = row.Amount,
                    DueDate = due
                });
            }

            try
            {
                db.SaveChanges();   // all new charges are saved together or not at all
            }
            catch (DbUpdateException)
            {
                Tell("Some of these rent charges were just created from another window, so nothing was saved.\n\n" +
                     "The list has been refreshed.");
                LoadList();
                return;
            }
        }

        LoadList();

        var message = $"Rent charges created: {count}.";
        if (partial > 0)
            message += $"\n\n{partial} charge(s) were billed by days (a mid-month move, or more than one stay this month).";
        Tell(message);
    }
}