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
        public string ArchivedText => IsArchived ? "Archived" : "";
        public bool Settled => IsVoided || Balance <= 0;
    }

    private const string AllItems = "All items";

    private List<ChargeRow> _all = new();
    private int? _selectedId;
    private bool _ready;
    private bool _binding;

    public BillingPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    // ---------- Helpers ----------

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

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

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
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
            Tell("There are no fully paid charges left to archive in this month.");
            return;
        }

        var month = (MonthCombo.SelectedItem as MonthOption)?.Display;
        var answer = MessageBox.Show(
            $"Archive {ids.Count} fully paid charge(s) for {month}?\n\n" +
            "They are only hidden from this list. Totals, reports and statements are not affected, " +
            "and you can bring them back with Show archived and Unarchive.",
            "Archive settled charges", MessageBoxButton.YesNo, MessageBoxImage.Question);
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

    private void ChargeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding) return;
        _selectedId = Selected?.Id;
        ShowDetail();
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

        using var db = new AppDbContext();
        var charges = db.Charges.IgnoreQueryFilters()   // look past the voided-charge filter
            .Include(c => c.UtilityBill)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .Include(c => c.Occupancy).ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .Where(c => c.BillingMonth == key && (showVoided || !c.IsVoided))
            .ToList();

        _all = charges
            .Select(c =>
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
                    IsVoided = c.IsVoided,
                    IsArchived = c.IsArchived,
                    Note = c.IsVoided ? $"Voided: {c.VoidReason}" : "",
                    Status = BillingRules.ChargeStatus(c.Amount, paid, c.DueDate, today, c.IsVoided),
                    LastPaid = c.Allocations.Count == 0
                        ? (DateOnly?)null
                        : c.Allocations.Max(a => a.Payment.PaymentDate),
                    Payments = c.Allocations
                        .Select(a => new PaidItem
                        {
                            Date = a.Payment.PaymentDate,
                            Method = a.Payment.Method ?? "",
                            Reference = a.Payment.ReferenceNo ?? "",
                            Amount = a.AmountApplied
                        })
                        .OrderByDescending(p => p.Date)
                        .ToList()
                };
            })
            .OrderBy(r => r.Tenant).ThenBy(r => r.Item == "Rent" ? 0 : 1).ThenBy(r => r.Item)
            .ToList();

        // The cards ignore voided charges and the filters.
        var live = _all.Where(r => !r.IsVoided).ToList();
        var billed = live.Sum(r => r.Amount);
        var paidTotal = live.Sum(r => r.Paid);
        var overdue = live.Where(r => r.Status == "Overdue").Sum(r => r.Balance);

        BilledCard.Value = Fmt.Peso(billed);
        BilledCard.Caption = $"{live.Count} charge(s)";

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

        BalanceCard.Value = Fmt.Peso(billed - paidTotal);
        BalanceCard.Caption = "Still to collect";
        BalanceCard.Tone = "Warning";

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
                _ => true
            })
            .Where(r => query == "" || r.Tenant.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _binding = true;   // ignore selection events while the list is being rebuilt
        ChargeGrid.ItemsSource = rows;
        ChargeGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == _selectedId);
        _binding = false;

        ShowDetail();
        UpdateButtons();
    }

    // ---------- Details: who paid what ----------

    private static void AddLine(Panel panel, string label, string value, string? tone = null)
    {
        var line = new TextBlock { Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
        line.Inlines.Add(new Run(label + "   ") { Foreground = Res("AppSecondaryBrush") });
        line.Inlines.Add(new Run(value)
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = tone == null ? Res("AppTextBrush") : Res(tone + "Brush")
        });
        panel.Children.Add(line);
    }

    private void ShowDetail()
    {
        var row = Selected;
        DetailEmpty.Visibility = row == null ? Visibility.Visible : Visibility.Collapsed;
        DetailBody.Visibility = row == null ? Visibility.Collapsed : Visibility.Visible;
        if (row == null) return;

        DetailTitle.Text = row.Tenant;
        DetailSub.Text = $"{row.Description}  -  {row.Bed}";
        DetailBadge.Content = row.Status;

        InfoPanel.Children.Clear();
        AddLine(InfoPanel, "Amount", Fmt.Peso(row.Amount));
        AddLine(InfoPanel, "Paid", Fmt.Peso(row.Paid), row.Paid > 0 ? "Success" : null);
        AddLine(InfoPanel, "Balance", Fmt.Peso(row.Balance),
            row.IsVoided ? null : row.Balance <= 0 ? "Success" : row.Status == "Overdue" ? "Danger" : "Warning");
        AddLine(InfoPanel, "Due", row.DueText);
        if (row.IsVoided) AddLine(InfoPanel, "Voided", row.Note.Replace("Voided: ", ""));

        PaymentList.Children.Clear();
        if (row.Payments.Count == 0)
        {
            PaymentList.Children.Add(new TextBlock
            {
                Text = row.IsVoided ? "This charge was voided." : "No payment has been applied to this charge yet.",
                Style = (Style)FindResource("MutedText"),
                TextWrapping = TextWrapping.Wrap
            });
        }
        foreach (var payment in row.Payments)
        {
            var parts = new List<string> { Fmt.Date(payment.Date) };
            if (payment.Method != "") parts.Add(payment.Method);
            if (payment.Reference != "") parts.Add("Ref " + payment.Reference);

            var item = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            item.Children.Add(new TextBlock
            {
                Text = string.Join("  -  ", parts),
                Foreground = Res("AppTextBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            var amount = new TextBlock
            {
                Text = Fmt.Peso(payment.Amount),
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("SuccessBrush"),
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(amount, 1);
            item.Children.Add(amount);

            PaymentList.Children.Add(item);
        }

        // This month for the same tenant, item by item: Rent, Electricity, Water, ...
        var month = (MonthCombo.SelectedItem as MonthOption)?.Display ?? "this month";
        SummaryTitle.Text = $"{row.Tenant} in {month}";
        SummaryPanel.Children.Clear();

        var mine = _all.Where(r => r.TenantId == row.TenantId && !r.IsVoided).ToList();

        foreach (var group in mine.GroupBy(r => r.Item)
                                  .OrderBy(g => g.Key == "Rent" ? 0 : 1).ThenBy(g => g.Key))
        {
            var billed = group.Sum(r => r.Amount);
            var paid = group.Sum(r => r.Paid);
            var left = billed - paid;
            var isOverdue = group.Any(r => r.Status == "Overdue");

            var text = left <= 0 ? "Paid in full"
                     : paid <= 0 ? $"Not paid yet ({Fmt.Peso(left)})"
                     : $"{Fmt.Peso(paid)} paid of {Fmt.Peso(billed)}";

            AddLine(SummaryPanel, group.Key, text,
                left <= 0 ? "Success" : isOverdue ? "Danger" : "Warning");
        }

        var totalBalance = mine.Sum(r => r.Balance);
        AddLine(SummaryPanel, "Still owed", Fmt.Peso(totalBalance), totalBalance <= 0 ? "Success" : "Warning");
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
            ? $"Amount: {Fmt.Peso(row.Amount)}. A utility share can't be edited here. To change it, undo the split on the Utility Bills page and split again."
            : row.Paid > 0
                ? $"Amount: {Fmt.Peso(row.Amount)}. The amount is locked because payments have been applied. Void the payment first if the amount is wrong."
                : null;

        var v = FormDialog.Ask(Owner, $"Edit charge - {row.Tenant}", "Save changes", fields.ToArray(),
            vals =>
            {
                if (vals[0].Trim() == "") return "Please enter a description.";
                if (!TryDate(vals[1], out _)) return "Enter the due date like 2026-10-05.";
                if (canEditAmount && (!TryMoney(vals[2], out var amount) || amount <= 0))
                    return "Enter the amount as a number greater than zero.";
                return null;
            }, note);
        if (v == null) return;

        using var db = new AppDbContext();
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
            Tell("This charge has payments applied to it.\n\n" +
                 "Void those payments first (Payments page, Payment history), then void the charge.");
            return;
        }

        var note = $"This cancels the {row.Description.ToLower()} of {Fmt.Peso(row.Amount)} for {row.Tenant}. " +
                   "It stays visible when you tick Show voided, and it no longer counts in any total. This can't be undone.";
        if (row.Type == ChargeType.Rent)
            note += " A voided rent charge is not created again when you generate rent.";
        if (row.Type == ChargeType.Utility)
            note += " Other tenants' shares of this bill are not changed.";

        var v = FormDialog.Ask(Owner, $"Void charge - {row.Tenant}", "Void charge",
            new[] { ("Reason for voiding", "") },
            vals => vals[0].Trim() == "" ? "Please enter a reason." : null,
            note);
        if (v == null) return;

        using var db = new AppDbContext();
        var charge = db.Charges.Include(c => c.Allocations).First(c => c.Id == row.Id);

        if (charge.Allocations.Count > 0)
        {
            Tell("A payment was just applied to this charge. Void that payment first.");
            LoadCharges();
            return;
        }

        charge.IsVoided = true;
        charge.VoidReason = v[0].Trim();
        charge.VoidedOn = DateOnly.FromDateTime(DateTime.Today);
        db.SaveChanges();

        LoadCharges();
    }
}