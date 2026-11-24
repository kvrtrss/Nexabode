using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class BillingPage : Page
{
    public class PaidItem
    {
        public DateOnly Date { get; set; }
        public string Method { get; set; } = "";
        public string ReceiptNo { get; set; } = "";
        public string Reference { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class ChargeRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Tenant { get; set; } = "";
        public string Bed { get; set; } = "";
        public string Item { get; set; } = "";
        public string Description { get; set; } = "";
        public ChargeType Type { get; set; }
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateOnly DueDate { get; set; }
        public string DueText => DueDate.ToString("MMM d, yyyy");
        public DateOnly? LastPaid { get; set; }
        public string LastPaidText => LastPaid?.ToString("MMM d, yyyy") ?? "-";
        public string Status { get; set; } = "";
        public bool IsVoided { get; set; }
        public string Note { get; set; } = "";
        public List<PaidItem> Payments { get; set; } = new();
        public bool IsArchived { get; set; }
        // One column instead of two: the description already says what the charge is, so the item is only
        // added in front when the description does not mention it.
        public string ChargeText => Description.Contains(Item, StringComparison.OrdinalIgnoreCase)
            ? Description : $"{Item}: {Description}";
        public string ArchivedText => IsArchived ? "Archived" : "";
        public bool Settled => IsVoided || Balance <= 0;
        public decimal Refunded { get; set; }
        public DateOnly? RefundedOn { get; set; }
        public string RefundDetail => Refunded <= 0 ? ""
            : Fmt.Peso(Refunded) + (RefundedOn is DateOnly d ? " on " + Fmt.Date(d) : "");
        public string NoteText => IsVoided ? Note : Refunded > 0 ? "Refunded " + RefundDetail : "";

        // Second line of the amount column, and the three lines that open under a selected row.
        public string BalanceLine => IsVoided ? "" : Balance <= 0 ? "Paid in full" : $"Balance {Fmt.Peso(Balance)}";
        public string DetailsMain { get; set; } = "";
        public string PaymentsText { get; set; } = "";

        public string Month { get; set; } = "";        // billing month key, "2026-11"
        public string MonthText => DateOnly.TryParseExact(Month + "-01", "yyyy-MM-dd", out var m)
            ? m.ToString("MMMM yyyy", CultureInfo.CurrentCulture) : Month;

        // Under a tenant's name the row does not repeat it: second line says whether and when it was paid.
        // Empty when there is nothing to say (the page then leaves the line out), instead of repeating "No payment yet".
        public string LineTwoGrouped => NoteText != "" ? NoteText
            : LastPaid is DateOnly paidOn ? $"Last paid {Fmt.Date(paidOn)}" : "";

        // "6 days late" next to the Overdue badge; empty for everything that is not overdue.
        public int DaysLate { get; set; }
        public string LateText => DaysLate > 0 ? Fmt.Count(DaysLate, "day") + " late" : "";

        public TenantGroupKey? GroupKey { get; set; }   // set by the page, so the charges of a tenant sit together
        public MonthGroupKey? MonthGroup { get; set; }  // set by the Tenant account window, so a month's charges sit together
    }

    // The heading above one month's charges in the Tenant account window.
    public sealed class MonthGroupKey : IEquatable<MonthGroupKey>
    {
        public string Month { get; init; } = "";
        public string Name { get; init; } = "";
        public string Summary { get; set; } = "";

        private bool _isExpanded = true;
        public Action<string, bool>? ExpandedChanged { get; set; }
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; ExpandedChanged?.Invoke(Month, value); }
        }

        public bool Equals(MonthGroupKey? other) => other != null && other.Month == Month;
        public override bool Equals(object? obj) => Equals(obj as MonthGroupKey);
        public override int GetHashCode() => Month.GetHashCode();
    }

    // The heading above one tenant's charges: their name and what they owe in the rows shown.
    public sealed class TenantGroupKey : IEquatable<TenantGroupKey>
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string Summary { get; set; } = "";

        // Whether this tenant's charges are open. The page remembers it, so a refresh does not reset it.
        private bool _isExpanded;
        public Action<int, bool>? ExpandedChanged { get; set; }
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; ExpandedChanged?.Invoke(Id, value); }
        }

        public bool Equals(TenantGroupKey? other) => other != null && other.Id == Id;
        public override bool Equals(object? obj) => Equals(obj as TenantGroupKey);
        public override int GetHashCode() => Id;
    }

    // One charge as a row, shared by this page and the Tenant account window.
    internal static ChargeRow ToRow(Charge c, DateOnly today)
    {
        var paid = c.Allocations.Sum(a => a.AmountApplied);
        var balance = c.Amount - paid;
        return new ChargeRow
        {
            Id = c.Id,
            TenantId = c.Occupancy.TenantId,
            Tenant = c.Occupancy.Tenant.FullName,
            Bed = $"{c.Occupancy.Bed.Room.RoomName} / {c.Occupancy.Bed.BedLabel}",
            Item = BillingHelpers.ItemOf(c),
            Description = c.Description ?? c.ChargeType.ToString(),
            Type = c.ChargeType,
            Amount = c.Amount,
            Paid = paid,
            Balance = balance,
            DueDate = c.DueDate,
            Month = c.BillingMonth,
            IsVoided = c.IsVoided,
            IsArchived = c.IsArchived,
            Note = c.IsVoided ? $"Voided: {c.VoidReason}" : "",
            Status = BillingRules.ChargeStatus(c.Amount, paid, c.DueDate, today, c.IsVoided, c.RefundedAmount),
            Refunded = c.RefundedAmount,
            RefundedOn = c.RefundedOn,
            DaysLate = BillingRules.ChargeStatus(c.Amount, paid, c.DueDate, today, c.IsVoided, c.RefundedAmount) == "Overdue"
                ? Math.Max(0, today.DayNumber - c.DueDate.DayNumber) : 0,
            LastPaid = c.Allocations.Count == 0
                ? (DateOnly?)null
                : c.Allocations.Max(a => a.Payment.PaymentDate),
            Payments = c.Allocations
                .Select(a => new PaidItem
                {
                    Date = a.Payment.PaymentDate,
                    Method = a.Payment.Method ?? "",
                    ReceiptNo = a.Payment.ReceiptNo ?? "",
                    Reference = a.Payment.ReferenceNo ?? "",
                    Amount = a.AmountApplied
                })
                .OrderByDescending(p => p.Date)
                .ToList()
        };
    }

    private const string AllItems = "All items";

    private List<ChargeRow> _all = new();
    private int? _selectedId;
    private bool _ready;
    private bool _binding;
    // Tenants whose charges the user has opened. Everything starts closed; a refresh (after closing the
    // Tenant account window, editing, filtering) keeps what was opened.
    private readonly HashSet<int> _expanded = new();
    private List<int> _shownTenants = new();   // tenants with a heading in the list right now (for Expand all)

    public BillingPage()
    {
        InitializeComponent();

        // Right-click on a row offers the same actions as the buttons.
        GridMenu.Attach(ChargeGrid, EditButton, VoidButton, ArchiveButton, UnarchiveButton);
        Loaded += (_, _) => Setup();
    }

    // ---------- Helpers ----------


    private Window? Owner => Window.GetWindow(this);

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out date);

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    private DateOnly? SelectedMonth => (MonthCombo.SelectedItem as MonthOption)?.Start;

    private ChargeRow? Selected =>
        ChargeGrid.SelectedItems.Count == 1 ? ChargeGrid.SelectedItem as ChargeRow : null;

    // ---------- Tenant account window, and the More menu ----------

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = MoreButton;
        menu.IsOpen = true;
    }

    private void OpenAccount(int tenantId)
    {
        var window = new TenantAccountWindow(tenantId);
        if (Window.GetWindow(this) is { } owner) window.Owner = owner;
        window.ShowDialog();
        LoadCharges();   // a payment may have been recorded in the window
    }

    // The "Tenant account" button on a tenant's heading.
    private void GroupAccount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: int tenantId }) OpenAccount(tenantId);
    }

    // Only a double-click on a charge (not on a heading or the empty area) opens the account.
    private void ChargeGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element != null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is DataGridRow { Item: ChargeRow row }) { OpenAccount(row.TenantId); return; }
            if (element is DataGrid) return;
        }
    }

    private List<ChargeRow> SelectedCharges =>
        ChargeGrid.SelectedItems.OfType<ChargeRow>().ToList();

    private static string ChoiceOf(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

    private void Setup()
    {
        var months = BillingHelpers.BuildMonths();
        MonthCombo.ItemsSource = months;
        _ready = true;
        MonthCombo.SelectedItem = months.First(m => m.Start == BillingHelpers.CurrentMonthStart());
    }

    // Previous / next month buttons next to the month list.
    private void StepMonth(int direction)
    {
        if (MonthCombo.ItemsSource is not IEnumerable<MonthOption> months || SelectedMonth is not DateOnly current) return;
        var target = direction < 0
            ? months.Where(m => m.Start < current).OrderByDescending(m => m.Start).FirstOrDefault()
            : months.Where(m => m.Start > current).OrderBy(m => m.Start).FirstOrDefault();
        if (target != null) MonthCombo.SelectedItem = target;
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e) => StepMonth(-1);

    private void NextMonth_Click(object sender, RoutedEventArgs e) => StepMonth(1);

    private void UpdateMonthButtons()
    {
        if (MonthCombo.ItemsSource is not IEnumerable<MonthOption> months || SelectedMonth is not DateOnly current)
        {
            PrevMonthButton.IsEnabled = NextMonthButton.IsEnabled = false;
            return;
        }
        PrevMonthButton.IsEnabled = months.Any(m => m.Start < current);
        NextMonthButton.IsEnabled = months.Any(m => m.Start > current);
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateMonthButtons();
        _selectedId = null;
        LoadCharges();
    }

    private void ShowVoided_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) LoadCharges();
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
        var ids = SelectedCharges
            .Where(r => r.IsArchived != archived && (!archived || r.Settled))
            .Select(r => r.Id)
            .ToList();
        ArchiveCharges(ids, archived);
    }

    private void ArchiveSettled_Click(object sender, RoutedEventArgs e)
    {
        var ids = _all.Where(r => !r.IsArchived && !r.IsVoided && r.Balance <= 0).Select(r => r.Id).ToList();
        if (ids.Count == 0)
        {
            Say.Info("There are no fully paid charges to archive for this month.");
            return;
        }

        var month = (MonthCombo.SelectedItem as MonthOption)?.Display;
        var answer = ConfirmActionDialog.YesNo(
            $"Archive {Fmt.Count(ids.Count, "fully paid charge")} for {month}?\n\n" +
            "They are only hidden from this list. Totals, reports and statements are not affected, " +
            "and you can bring them back with Show archived and Unarchive.",
            "Archive settled charges", confirmText: "Archive");
        if (answer != MessageBoxResult.Yes) return;

        ArchiveCharges(ids, true);
    }

    private void ArchiveCharges(List<int> ids, bool archived)
    {
        if (ids.Count == 0) return;

        using var db = AppDb.Create();
        foreach (var charge in db.Charges.IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToList())
            charge.IsArchived = archived;
        db.SaveChanges();

        LoadCharges();
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_binding) ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) ApplyFilter();
    }

    // Closing a tenant's heading hides its rows, so charges selected inside it are deselected
    // (otherwise Edit / Void would act on a row nobody can see).
    private void GroupToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_binding) return;
        if (sender is not FrameworkElement { DataContext: System.Windows.Data.CollectionViewGroup { Name: TenantGroupKey key } }) return;
        foreach (var row in ChargeGrid.SelectedItems.OfType<ChargeRow>().Where(r => r.TenantId == key.Id).ToList())
            ChargeGrid.SelectedItems.Remove(row);
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var id in _shownTenants) _expanded.Add(id);
        ApplyFilter();
    }

    private void CollapseAll_Click(object sender, RoutedEventArgs e)
    {
        _expanded.Clear();
        _selectedId = null;
        ApplyFilter();
    }

    // The summary cards double as filters: click one to see just those charges, click it again for everything.
    private void SetStatusChoice(string choice)
    {
        foreach (var item in StatusFilter.Items.OfType<ComboBoxItem>())
            if (item.Content?.ToString() == choice) { StatusFilter.SelectedItem = item; return; }
    }

    private void CardFilter(string choice) =>
        SetStatusChoice(ChoiceOf(StatusFilter) == choice ? "All statuses" : choice);

    private void BilledCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SetStatusChoice("All statuses");
    private void PaidCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CardFilter("Fully paid");
    private void BalanceCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CardFilter("Not fully paid");
    private void OverdueCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CardFilter("Overdue");

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        _binding = true;   // one rebuild at the end, not one per box
        ItemFilter.SelectedItem = AllItems;
        StatusFilter.SelectedIndex = 0;
        _binding = false;
        SearchBox.Text = "";
        ApplyFilter();
    }

    private void ChargeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding) return;
        _selectedId = Selected?.Id;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var row = Selected;
        EditButton.IsEnabled = row is { IsVoided: false };
        VoidButton.IsEnabled = row is { IsVoided: false };

        var rows = SelectedCharges;
        ArchiveButton.IsEnabled = rows.Any(r => !r.IsArchived && r.Settled);
        UnarchiveButton.IsEnabled = rows.Any(r => r.IsArchived);
    }

    // ---------- Loading ----------

    private void LoadCharges()
    {
        if (SelectedMonth is not DateOnly start) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var key = BillingHelpers.MonthKey(start);
        var showVoided = ShowVoidedCheck.IsChecked == true;

        using var db = AppDb.Create();
        var charges = db.Charges.IgnoreQueryFilters()   // look past the voided-charge filter
            .Include(c => c.UtilityBill)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Include(c => c.Occupancy).ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .Where(c => c.BillingMonth == key)
            .ToList()
            .Where(c => showVoided || !c.IsVoided || c.RefundedAmount > 0)
            .ToList();

        _all = charges
            .Select(c => ToRow(c, today))
            .OrderBy(r => r.Tenant).ThenBy(r => r.Item == "Rent" ? 0 : 1).ThenBy(r => r.Item)
            .ToList();

        // The cards ignore voided charges and the filters.
        var live = _all.Where(r => !r.IsVoided).ToList();
        var billed = live.Sum(r => r.Amount);
        var paidTotal = live.Sum(r => r.Paid);
        var overdue = live.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        BilledCard.Value = Fmt.Peso(billed);
        BilledCard.Caption = Fmt.Count(live.Count, "charge");

        PaidCard.Value = Fmt.Peso(paidTotal);
        if (billed == 0)
        {
            PaidCard.Caption = "Nothing billed yet";
        }
        else
        {
            // Never show 100% until everything billed has actually been collected.
            var percent = paidTotal >= billed
                ? 100m
                : Math.Min(99m, Math.Round(paidTotal * 100 / billed, MidpointRounding.AwayFromZero));
            PaidCard.Caption = $"{percent:0}% collected";
        }
        PaidCard.Tone = "Success";

        var balance = billed - paidTotal;
        BalanceCard.Value = Fmt.Peso(balance);
        if (balance <= 0)
        {
            BalanceCard.Caption = "Nothing left to collect";
            BalanceCard.Tone = "Success";
        }
        else if (overdue >= balance)
        {
            BalanceCard.Caption = "All of it is past due";   // the Overdue card would show the same number
            BalanceCard.Tone = "Danger";
        }
        else
        {
            BalanceCard.Caption = "Still to collect";
            BalanceCard.Tone = "Warning";
        }

        // The Overdue card only appears when it says something the Balance card does not.
        var showOverdue = overdue != balance;
        OverdueCard.Visibility = showOverdue ? Visibility.Visible : Visibility.Collapsed;
        StatsGrid.Columns = showOverdue ? 4 : 3;
        OverdueCard.Value = Fmt.Peso(overdue);
        OverdueCard.Caption = overdue > 0 ? "Past due date" : "Nothing overdue";
        OverdueCard.Tone = overdue > 0 ? "Danger" : "Success";

        RebuildItemFilter();
        ApplyFilter();
    }

    // The item filter lists whatever is billed that month: Rent, Electricity, Water, ...
    private void RebuildItemFilter()
    {
        var keep = ItemFilter.SelectedItem as string ?? AllItems;

        var items = new List<string> { AllItems };
        items.AddRange(_all.Select(r => r.Item).Distinct()
            .OrderBy(i => i == "Rent" ? 0 : 1).ThenBy(i => i));

        _binding = true;
        ItemFilter.ItemsSource = items;
        ItemFilter.SelectedItem = items.Contains(keep) ? keep : AllItems;
        _binding = false;
    }

    private void ApplyFilter()
    {
        FillDetailText();
        var item = ItemFilter.SelectedItem as string ?? AllItems;
        var status = ChoiceOf(StatusFilter);
        var query = SearchBox.Text.Trim();
        var showArchived = ShowArchivedCheck.IsChecked == true;

        var rows = _all
            .Where(r => showArchived || !(r.IsArchived && r.Settled))   // archived rows hide only while settled
            .Where(r => item == AllItems || r.Item == item)
            .Where(r => status switch
            {
                "Not fully paid" => !r.IsVoided && r.Balance > 0,
                "Fully paid" => r.Status == "Paid",
                "Overdue" => r.Status == "Overdue",
                "Refunded" => r.Refunded > 0,
                _ => true
            })
            .Where(r => query == "" || r.Tenant.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Tenants with the most overdue come first, then the most owed, then by name.
        rows = rows.GroupBy(r => r.TenantId)
            .Select(g => new
            {
                Rows = g,
                Name = g.First().Tenant,
                Overdue = g.Where(r => !r.IsVoided && r.Status == "Overdue").Sum(r => r.Balance),
                Owed = g.Where(r => !r.IsVoided).Sum(r => r.Balance)
            })
            .OrderByDescending(t => t.Overdue).ThenByDescending(t => t.Owed)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .SelectMany(t => t.Rows)
            .ToList();
        _shownTenants = rows.Select(r => r.TenantId).Distinct().ToList();

        ClearFiltersButton.Visibility = item != AllItems || status != "All statuses" || query != ""
            ? Visibility.Visible : Visibility.Collapsed;
        var anyShown = _shownTenants.Count > 0;
        ExpandAllButton.IsEnabled = CollapseAllButton.IsEnabled = anyShown;

        // One heading per tenant, with what they owe in the rows shown.
        var keys = new Dictionary<int, TenantGroupKey>();
        foreach (var group in rows.GroupBy(r => r.TenantId))
        {
            var live = group.Where(r => !r.IsVoided).ToList();
            var owed = live.Sum(r => r.Balance);
            var overdue = live.Where(r => r.Status == "Overdue").Sum(r => r.Balance);
            var summary = Fmt.Count(group.Count(), "charge") + "   ·   " +
                          (owed > 0 ? $"Balance {Fmt.Peso(owed)}" : "Paid in full") +
                          (overdue > 0 ? $"   ·   Overdue {Fmt.Peso(overdue)}" : "");
            var key = new TenantGroupKey { Id = group.Key, Name = group.First().Tenant, Summary = summary };
            key.IsExpanded = _expanded.Contains(group.Key);   // set before the callback, so loading changes nothing
            key.ExpandedChanged = (id, open) => { if (open) _expanded.Add(id); else _expanded.Remove(id); };
            keys[group.Key] = key;
            foreach (var row in group) row.GroupKey = keys[group.Key];
        }

        var view = new System.Windows.Data.ListCollectionView(rows);
        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ChargeRow.GroupKey)));

        _binding = true;   // ignore selection events while the list is being rebuilt
        ChargeGrid.ItemsSource = view;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ChargeGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == _selectedId && _expanded.Contains(r.TenantId));
        _binding = false;

        UpdateButtons();
    }

    // ---------- Details: who paid what (shown under the selected row) ----------

    // Fills in the lines that open under a selected row: the numbers, and what was paid on this charge.
    private void FillDetailText()
    {
        foreach (var row in _all)
        {
            var main = new List<string>
            {
                $"Amount {Fmt.Peso(row.Amount)}",
                $"Paid {Fmt.Peso(row.Paid)}",
                $"Balance {Fmt.Peso(row.Balance)}",
                $"Due {row.DueText}"
            };
            if (row.LastPaid is DateOnly lastPaid) main.Add($"Last paid {Fmt.Date(lastPaid)}");
            if (row.IsVoided) main.Add("Voided: " + row.Note.Replace("Voided: ", ""));
            if (row.Refunded > 0) main.Add("Refunded " + row.RefundDetail);
            if (row.IsArchived) main.Add("Archived");
            row.DetailsMain = string.Join("     ", main);

            if (row.Payments.Count == 0)
            {
                row.PaymentsText = row.IsVoided ? "This charge was voided." : "No payment has been applied to this charge yet.";
            }
            else
            {
                row.PaymentsText = "Payments on this charge:\n" + string.Join("\n", row.Payments.Select(payment =>
                {
                    var parts = new List<string> { Fmt.Date(payment.Date) };
                    if (payment.ReceiptNo != "") parts.Add(payment.ReceiptNo);
                    if (payment.Method != "") parts.Add(payment.Method);
                    if (payment.Reference != "") parts.Add("Ref " + payment.Reference);
                    return string.Join("  ·  ", parts) + "   " + Fmt.Peso(payment.Amount);
                }));
            }
        }
    }

    // ---------- Edit and void ----------

    private void EditCharge_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsVoided) return;

        var canEditAmount = row.Type != ChargeType.Utility && row.Paid == 0;

        var fields = new List<(string Label, string Value)>
        {
            ("Description", row.Description),
            ("Due date (e.g. 2026-10-05)", row.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        };
        if (canEditAmount)
            fields.Add(("Amount", row.Amount.ToString("0.##", CultureInfo.CurrentCulture)));

        var note = row.Type == ChargeType.Utility
            ? $"Amount: {Fmt.Peso(row.Amount)}. A utility share cannot be edited here. To change it, undo the split on the Utility Bills page, then split the bill again."
            : row.Paid > 0
                ? $"Amount: {Fmt.Peso(row.Amount)}. The amount cannot be changed because a payment has been applied to this charge. If the amount is wrong, void the payment first."
                : null;

        var v = FormDialog.Ask(Owner, $"Edit charge: {row.Tenant}", "Save changes", fields.ToArray(),
            vals =>
            {
                if (vals[0].Trim() == "") return "Enter a description.";
                if (!TryDate(vals[1], out _)) return "Enter the due date in the format 2026-10-05.";
                if (canEditAmount && (!TryMoney(vals[2], out var amount) || amount <= 0))
                    return "Enter the amount as a number greater than zero.";
                return null;
            }, note);
        if (v == null) return;

        // A short review of what changes. If nothing did, there is nothing to save.
        {
            TryDate(v[1], out var reviewDue);
            var reviewFields = new List<(string Field, string? Old, string? New)>
            {
                ("Description", row.Description, v[0]),
                ("Due date", Fmt.Date(row.DueDate), Fmt.Date(reviewDue))
            };
            if (canEditAmount)
            {
                TryMoney(v[2], out var reviewAmount);
                reviewFields.Add(("Amount", Fmt.Peso(row.Amount), Fmt.Peso(Math.Round(reviewAmount, 2))));
            }
            var reviewList = ConfirmWording.Changes(reviewFields.ToArray());
            if (reviewList.Count == 0) return;
            if (!ConfirmActionDialog.Ask(Owner, "Edit charge",
                    ConfirmWording.EditRecord($"{row.Tenant}'s {row.Description.ToLower()}", reviewList), "Save changes")) return;
        }

        using var db = AppDb.Create();
        var charge = db.Charges.First(c => c.Id == row.Id);
        charge.Description = Blank(v[0]);
        TryDate(v[1], out var due);
        charge.DueDate = due;
        if (canEditAmount)
        {
            TryMoney(v[2], out var amount);
            charge.Amount = Math.Round(amount, 2);
        }
        db.SaveChanges();

        LoadCharges();
    }

    private void VoidCharge_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null || row.IsVoided) return;

        if (row.Paid > 0)
        {
            Say.Info("This charge cannot be voided because payments have been applied to it.\n\n" +
                 "Void those payments first (Payments page, Payment history), then void the charge.");
            return;
        }

        var note = $"This cancels the {row.Description.ToLower()} of {Fmt.Peso(row.Amount)} for {row.Tenant}. " +
                   "It remains visible when Show voided is ticked, and it no longer counts in any total. This cannot be undone.";
        if (row.Type == ChargeType.Rent)
            note += " A voided rent charge is not created again when you generate rent.";
        if (row.Type == ChargeType.Utility)
            note += " Other tenants' shares of this bill are not changed.";

        var v = FormDialog.Ask(Owner, $"Void charge: {row.Tenant}", "Void charge",
            new[] { ("Reason for voiding", "") },
            vals => ReasonRules.Error(vals[0], "Enter a reason for voiding the charge (at least 3 letters or digits)."),
            note);
        if (v == null) return;

        using var db = AppDb.Create();
        var charge = db.Charges.Include(c => c.Allocations).Include(c => c.Occupancy).First(c => c.Id == row.Id);

        if (charge.Allocations.Count > 0)
        {
            Say.Info("A payment was just applied to this charge, so it cannot be voided. Void that payment first, then try again.");
            LoadCharges();
            return;
        }

        charge.IsVoided = true;
        charge.VoidReason = v[0].Trim();
        charge.VoidedOn = DateOnly.FromDateTime(DateTime.Today);
        AuditService.Record(db, "Charge", charge.Id, charge.Occupancy.TenantId, $"{row.Description}: voided",
                            StayEditService.MoneyText(charge.Amount), null, charge.VoidReason);
        db.SaveChanges();

        LoadCharges();
    }
}