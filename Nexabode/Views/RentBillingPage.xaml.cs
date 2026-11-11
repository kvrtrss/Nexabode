using System.Globalization;
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
        public int? ChargeId { get; set; }
        public bool IsArchived { get; set; }
        public bool Settled => Generated && (IsVoided || Balance <= 0);
        public string Status => IsVoided ? "Voided"
                              : IsArchived && Settled ? "Archived"
                              : Generated ? "Generated" : "Not generated";
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

    private Window? Owner => Window.GetWindow(this);

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out date);

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

    private void Setup()
    {
        var months = BillingHelpers.BuildMonths();
        MonthCombo.ItemsSource = months;
        FullRentCheck.IsChecked = AppSettings.Load().FullRentForPartialMonths;
        _ready = true;
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || SelectedMonth is not DateOnly start) return;
        RentDuePicker.SelectedDate = start.AddDays(4).ToDateTime(TimeOnly.MinValue);   // the 5th
        LoadList();
    }

    private void FullRent_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var full = FullRentCheck.IsChecked == true;
        AppSettings.Update(s => s.FullRentForPartialMonths = full);   // remembered for the whole property
        LoadList();
    }

    private void ShowArchived_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        var keep = SelectedMonth;
        var months = ShowArchivedCheck.IsChecked == true
            ? BillingHelpers.BuildMonthsWithData()
            : BillingHelpers.BuildMonths();

        MonthCombo.ItemsSource = months;
        MonthCombo.SelectedItem = months.FirstOrDefault(m => m.Start == keep)
                                  ?? months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    private void Archive_Click(object sender, RoutedEventArgs e) => SetArchived(true);

    private void Unarchive_Click(object sender, RoutedEventArgs e) => SetArchived(false);

    private void SetArchived(bool archived)
    {
        var ids = SelectedRows
            .Where(r => r.ChargeId != null && r.IsArchived != archived && (!archived || r.Settled))
            .Select(r => r.ChargeId!.Value)
            .ToList();
        ArchiveCharges(ids, archived);
    }

    private void ArchiveSettled_Click(object sender, RoutedEventArgs e)
    {
        var ids = _rows
            .Where(r => r.ChargeId != null && !r.IsArchived && !r.IsVoided && r.Settled)
            .Select(r => r.ChargeId!.Value)
            .ToList();
        if (ids.Count == 0)
        {
            Tell("There are no fully paid rent charges to archive for this month.");
            return;
        }

        var month = (MonthCombo.SelectedItem as MonthOption)?.Display;
        var answer = ConfirmActionDialog.YesNo(
            $"Archive {ids.Count} fully paid rent charge(s) for {month}?\n\n" +
            "They are only hidden from this list. Totals, reports and statements are not affected, " +
            "and the rent still counts as generated. You can show the charges again with Show archived and Unarchive.",
            "Archive settled rent", confirmText: "Archive");
        if (answer != MessageBoxResult.Yes) return;

        ArchiveCharges(ids, true);
    }

    private void ArchiveCharges(List<int> ids, bool archived)
    {
        if (ids.Count == 0) return;

        using var db = new AppDbContext();
        foreach (var charge in db.Charges.IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToList())
            charge.IsArchived = archived;
        db.SaveChanges();

        LoadList();
    }

    private void ShowVoided_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) LoadList();
    }

    private void RentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private RentRow? SelectedRow =>
        RentGrid.SelectedItems.Count == 1 ? RentGrid.SelectedItem as RentRow : null;

    private List<RentRow> SelectedRows =>
        RentGrid.SelectedItems.OfType<RentRow>().ToList();

    private void UpdateButtons()
    {
        GenerateSelectedButton.IsEnabled = SelectedRow is { Generated: false, IsVoided: false };
        EditRentButton.IsEnabled = SelectedRow is { Generated: true, IsVoided: false, ChargeId: not null };
        RestoreRentButton.IsEnabled = SelectedRow is { Generated: true, IsVoided: true, ChargeId: not null };

        var rows = SelectedRows;
        ArchiveButton.IsEnabled = rows.Any(r => r.ChargeId != null && !r.IsArchived && r.Settled);
        UnarchiveButton.IsEnabled = rows.Any(r => r.IsArchived);
    }

    // ---------- The list: what rent exists and what is still to be generated ----------

    private static List<RentRow> BuildRows(AppDbContext db, DateOnly start, bool fullRentForPartial)
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
                    ChargeId = charge.Id,
                    IsArchived = charge.IsArchived,
                    DueDate = charge.DueDate
                });
                continue;
            }

            // Partial months are billed by days unless the user chose full rent. A tenant with more
            // than one stay in the month is always billed by days, so they never pay full rent twice.
            var byDays = !fullRentForPartial || multiStayTenants.Contains(stay.TenantId);
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
        _rows = BuildRows(db, start, FullRentCheck.IsChecked == true);
        var showVoided = ShowVoidedCheck.IsChecked == true;
        var showArchived = ShowArchivedCheck.IsChecked == true;
        RentGrid.ItemsSource = _rows
            .Where(r => showVoided || !r.IsVoided)
            .Where(r => showArchived || !(r.IsArchived && r.Settled))
            .ToList();

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
        // When every row is voided, say so: otherwise the page looks as if nobody stayed this month.
        var voidedCount = _rows.Count(r => r.IsVoided);
        GeneratedCard.Caption = live.Count == 0 && voidedCount > 0
                                    ? (showVoided ? $"{voidedCount} voided: select one to restore it"
                                                  : $"{voidedCount} voided: tick Show voided to see them")
                              : live.Count == 0 ? "No stays this month"
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

    // ---------- Bring back a voided rent charge ----------

    // Voiding rent is final on purpose (a free month, a charge made in error), so a voided charge is never
    // created again by itself. When it was the wrong call, or the stay changed afterwards, this restores it:
    // the same charge becomes active again, updated to what the stay would be billed now.
    private void RestoreRent_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not RentRow { Generated: true, IsVoided: true, ChargeId: int chargeId }) return;

        var fullRent = FullRentCheck.IsChecked == true;
        var today = DateOnly.FromDateTime(DateTime.Today);

        RentRestorePlan plan;
        using (var look = new AppDbContext())
        {
            var (found, problem) = RentRestore.Plan(look, chargeId, fullRent, today);
            if (found == null) { Tell(problem ?? "That rent charge could not be restored."); return; }
            plan = found;
        }

        var answers = FormDialog.Ask(Owner, $"Restore rent: {plan.Tenant}", "Restore charge",
            new[] { ("Reason for restoring", "") },
            vals => RentRestoreRules.ReasonError(vals[0]),
            RentRestoreRules.ConfirmNote(plan.Tenant, plan.Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                                         plan.OldAmount, plan.OldDescription, plan.NewAmount,
                                         plan.NewDescription, plan.NewDueDate));
        if (answers == null) return;

        try
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            // Work it out again at the last moment, in case something changed while the window was open.
            var (fresh, problem) = RentRestore.Plan(db, chargeId, fullRent, today);
            if (fresh == null)
            {
                transaction.Rollback();
                Tell(problem ?? "That rent charge could not be restored.");
                LoadList();
                return;
            }

            RentRestore.Apply(db, fresh, answers[0].Trim());
            db.SaveChanges();
            transaction.Commit();
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The charge could not be restored because the data changed at the same moment. " +
                 "Nothing was changed. Please try again.");
        }

        LoadList();
    }

    // ---------- Edit a generated rent charge ----------

    private void EditRent_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not RentRow { Generated: true, IsVoided: false, ChargeId: int chargeId } row) return;

        // The amount is locked once money has been paid on the charge, the same rule as Billing Charges.
        var canEditAmount = row.Paid == 0;

        var fields = new List<(string Label, string Value)>
        {
            ("Details", row.Details),
            ("Due date (e.g. 2026-10-05)",
                (row.DueDate ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        };
        if (canEditAmount)
            fields.Add(("Amount", row.Amount.ToString("0.##", CultureInfo.CurrentCulture)));
        fields.Add(("Reason for the change", ""));
        var reasonIndex = fields.Count - 1;

        var note = canEditAmount
            ? $"Rent: {Fmt.Peso(row.Amount)}. Only this charge is changed. If you later edit the tenant's stay, " +
              "Nexabode compares this charge with the rent calculated for the new dates and may suggest an adjustment."
            : $"Rent: {Fmt.Peso(row.Amount)}. The amount cannot be changed because a payment has been applied to this charge. " +
              "If the amount is wrong, void the payment first.";

        var v = FormDialog.Ask(Owner, $"Edit rent: {row.Tenant}", "Save changes", fields.ToArray(),
            vals =>
            {
                if (vals[0].Trim() == "") return "Enter the details of the charge.";
                if (!TryDate(vals[1], out _)) return "Enter the due date in the format 2026-10-05.";
                if (canEditAmount && (!TryMoney(vals[2], out var amount) || Math.Round(amount, 2) <= 0))
                    return "Enter the amount as a number greater than zero.";
                if (!ReasonRules.IsReal(vals[reasonIndex])) return "Enter a reason for the change (at least 3 letters or digits).";
                return null;
            }, note);
        if (v == null) return;

        // A short review of what changes. If nothing did, there is nothing to save.
        {
            TryDate(v[1], out var reviewDue);
            var reviewFields = new List<(string Field, string? Old, string? New)>
            {
                ("Details", row.Details, v[0]),
                ("Due date", row.DueDate is DateOnly oldDue ? Fmt.Date(oldDue) : "(empty)", Fmt.Date(reviewDue))
            };
            if (canEditAmount)
            {
                TryMoney(v[2], out var reviewAmount);
                reviewFields.Add(("Amount", Fmt.Peso(row.Amount), Fmt.Peso(Math.Round(reviewAmount, 2))));
            }
            var reviewList = ConfirmWording.Changes(reviewFields.ToArray());
            if (reviewList.Count == 0) return;
            if (!ConfirmActionDialog.Ask(Owner, "Edit rent",
                    ConfirmWording.EditRecord($"{row.Tenant}'s rent", reviewList), "Save changes")) return;
        }

        using var db = new AppDbContext();
        var charge = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy)
            .FirstOrDefault(c => c.Id == chargeId);

        if (charge == null)
        {
            Tell("This rent charge has been voided, so it cannot be edited.");
            LoadList();
            return;
        }
        if (canEditAmount && charge.Allocations.Count > 0)
        {
            Tell("A payment was just applied to this charge, so the amount cannot be changed. Nothing was saved.");
            LoadList();
            return;
        }

        var reason = v[reasonIndex].Trim();
        var key = charge.BillingMonth;
        var tenantId = charge.Occupancy.TenantId;
        var changes = 0;

        void Change(string field, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            AuditService.Record(db, "Charge", charge.Id, tenantId, $"Rent {key}: {field}", oldValue, newValue, reason);
            changes++;
        }

        var newDetails = v[0].Trim();
        if (newDetails != (charge.Description ?? "Monthly rent"))
        {
            Change("description", charge.Description, newDetails);
            charge.Description = newDetails;
        }

        TryDate(v[1], out var newDue);
        Change("due date", StayEditService.DateText(charge.DueDate), StayEditService.DateText(newDue));
        charge.DueDate = newDue;

        if (canEditAmount)
        {
            TryMoney(v[2], out var typed);
            var newAmount = Math.Round(typed, 2);
            Change("amount", StayEditService.MoneyText(charge.Amount), StayEditService.MoneyText(newAmount));
            charge.Amount = newAmount;
        }

        if (changes == 0)
        {
            Tell("No changes were made.");
            return;
        }

        db.SaveChanges();   // the edit and its history entries are saved together or not at all
        LoadList();
    }

    // ---------- Generate ----------

    private void GenerateAll_Click(object sender, RoutedEventArgs e)
    {
        var todo = _rows.Where(r => !r.Generated && !r.IsVoided).ToList();

        if (todo.Count == 0)
        {
            Tell(_rows.Count == 0
                ? "No tenants stayed during this month, so there is no rent to generate."
                : "All rent for this month has already been generated.");
            return;
        }

        Generate(todo);
    }

    private void GenerateSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not RentRow { Generated: false, IsVoided: false } row)
        {
            Tell("Select a tenant whose rent has not been generated yet.");
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

        var confirm = ConfirmActionDialog.YesNo(
            $"Create {todo.Count} rent charge(s) for {monthName}, totalling {Fmt.Peso(todo.Sum(r => r.Amount))}, " +
            $"due {Fmt.Date(due)}?\n\nA charge can be voided later in Billing Charges if it was created by mistake.",
            "Generate rent", confirmText: "Generate rent");
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
                Tell("The rent charges could not be saved and nothing was saved. Some of them may already exist for this month.\n\n" +
                     "The list has been refreshed. Check it and try again.");
                LoadList();
                return;
            }
        }

        LoadList();

        var message = $"Rent charges created: {count}.";
        if (partial > 0)
            message += $"\n\n{partial} charge(s) were calculated by days (a move-in or move-out during the month, or more than one stay this month).";
        Tell(message);
    }
}