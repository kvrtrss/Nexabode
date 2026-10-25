using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;
using System.Globalization;

namespace Nexabode.Views;

public partial class TenantsPage : Page
{
    public class StayInfo
    {
        public int Id { get; set; }
        public string Bed { get; set; } = "";
        public DateOnly MoveIn { get; set; }
        public DateOnly? MoveOut { get; set; }
        public decimal Rent { get; set; }
        public decimal Deposit { get; set; }
        public decimal DepositRefunded { get; set; }
        public DateOnly? DepositRefundedOn { get; set; }
    }

    public class TenantRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string? Contact { get; set; }
        public string? EmergencyContact { get; set; }
        public string? SchoolOrWork { get; set; }
        public string? Notes { get; set; }
        public string CurrentBed { get; set; } = "-";
        public string MoveInText { get; set; } = "-";
        public string Status { get; set; } = "";
        public decimal Balance { get; set; }
        public decimal Overdue { get; set; }
        public int? OccupancyId { get; set; }
        public DateOnly? ActiveMoveIn { get; set; }
        public List<StayInfo> Stays { get; set; } = new();
    }

    private List<TenantRow> _all = new();
    private int? _selectedId;
    private bool _ready;
    private bool _binding;

    public TenantsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _ready = true;
            Reload();
        };
    }

    // ---------- Helpers ----------

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private Window? Owner => Window.GetWindow(this);

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool TryMoney(string text, out decimal value) =>
    decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value >= 0;

    private TenantRow? Current => TenantGrid.SelectedItem as TenantRow;

    private static string? NameError(string name, int exceptId)
    {
        var clean = name.Trim();
        if (clean == "") return "Please enter the tenant's full name.";

        var lower = clean.ToLower();
        using var db = new AppDbContext();
        return db.Tenants.Any(t => t.FullName.ToLower() == lower && t.Id != exceptId)
            ? "A tenant with this name already exists. If they are a different person, add a detail to tell them apart (for example \"Juan Cruz Jr.\")."
            : null;
    }

    // ---------- Loading ----------

    private void Reload(int? select = null)
    {
        if (select != null) _selectedId = select;

        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();

        var tenants = db.Tenants.OrderBy(t => t.FullName).ToList();

        var stays = db.Occupancies
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .ToList()
            .GroupBy(o => o.TenantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.MoveInDate).ToList());

        var charges = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy)
            .ToList()
            .GroupBy(c => c.Occupancy.TenantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        _all = tenants.Select(t =>
        {
            stays.TryGetValue(t.Id, out var list);
            list ??= new();
            charges.TryGetValue(t.Id, out var tenantCharges);
            tenantCharges ??= new();

            var currentStays = list
                .Where(o => o.MoveOutDate == null || o.MoveOutDate >= today)
                .OrderBy(o => o.MoveInDate)
                .ToList();
            var active = currentStays.FirstOrDefault();
            var status = active != null
                ? (active.MoveInDate > today ? "Reserved" : "Staying")
                : list.Count > 0 ? "Moved out" : "No stay yet";

            var balance = tenantCharges.Sum(c => c.Amount - c.Allocations.Sum(a => a.AmountApplied));
            var overdue = tenantCharges
                .Where(c => c.DueDate < today)
                .Sum(c => Math.Max(0, c.Amount - c.Allocations.Sum(a => a.AmountApplied)));

            static string BedName(Occupancy o) => $"{o.Bed.Room.RoomName} / {o.Bed.BedLabel}";

            return new TenantRow
            {
                Id = t.Id,
                FullName = t.FullName,
                Contact = t.Contact,
                EmergencyContact = t.EmergencyContact,
                SchoolOrWork = t.SchoolOrWork,
                Notes = t.Notes,
                Status = status,
                Balance = balance,
                Overdue = overdue,
                CurrentBed = active != null
                    ? BedName(active)
                      + (active.MoveOutDate is DateOnly until ? $" (until {Fmt.Date(until)})" : "")
                      + (currentStays.Count > 1 ? $"  +{currentStays.Count - 1} more stay(s)" : "")
                    : "-",
                MoveInText = active != null ? Fmt.Date(active.MoveInDate) : "-",
                OccupancyId = active?.Id,
                ActiveMoveIn = active?.MoveInDate,
                Stays = list.Select(o => new StayInfo
                {
                    Id = o.Id,
                    Bed = BedName(o),
                    MoveIn = o.MoveInDate,
                    MoveOut = o.MoveOutDate,
                    Rent = o.AgreedRent,
                    Deposit = o.Deposit,
                    DepositRefunded = o.DepositRefunded,
                    DepositRefundedOn = o.DepositRefundedOn
                }).ToList()
            };
        }).ToList();

        UpdateStats();
        ApplyFilter();
    }

    private void UpdateStats()
    {
        var owing = _all.Count(r => r.Balance > 0);
        var overdue = _all.Sum(r => r.Overdue);

        TotalCard.Value = _all.Count.ToString();
        TotalCard.Caption = _all.Count == 0 ? "Add your first tenant" : "tenant record(s)";
        TotalCard.Tone = "Info";

        StayingCard.Value = _all.Count(r => r.Status == "Staying").ToString();
        StayingCard.Caption = "currently in a bed";
        StayingCard.Tone = "Success";

        ReservedCard.Value = _all.Count(r => r.Status == "Reserved").ToString();
        ReservedCard.Caption = "moving in soon";
        ReservedCard.Tone = "Warning";

        BalanceCard.Value = Fmt.Peso(_all.Sum(r => r.Balance));
        BalanceCard.Caption = owing == 0 ? "Everyone is settled" : $"{owing} tenant(s) owe money";
        BalanceCard.Tone = overdue > 0 ? "Danger" : owing > 0 ? "Warning" : "Success";
    }

    private void ApplyFilter()
    {
        var filter = (FilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All tenants";
        var query = SearchBox.Text.Trim();
        var showArchived = ShowArchivedCheck.IsChecked == true;

        var rows = _all
            .Where(r => filter != "All tenants"      // a specific filter always shows what it names
                     || showArchived
                     || query != ""                  // searching looks through everyone
                     || r.Id == _selectedId          // keep the tenant you are working on in view
                     || !IsArchived(r))
            .Where(r => filter switch
            {
                "Staying" => r.Status == "Staying",
                "Reserved" => r.Status == "Reserved",
                "Moved out" => r.Status == "Moved out",
                "No stay yet" => r.Status == "No stay yet",
                "Has balance" => r.Balance > 0,
                _ => true
            })
            .Where(r => query == ""
                || r.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (r.Contact ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.CurrentBed.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _binding = true;   // ignore selection events while the list is being rebuilt
        TenantGrid.ItemsSource = rows;
        TenantGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == _selectedId);
        _binding = false;

        ShowDetail();
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) ApplyFilter();
    }

    // A moved-out tenant with nothing owed is "archived": hidden from the default list, never deleted.
    private static bool IsArchived(TenantRow r) => r.Status == "Moved out" && r.Balance <= 0;

    private void ShowArchived_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) ApplyFilter();
    }

    private void TenantGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding) return;
        _selectedId = Current?.Id;
        ShowDetail();
    }

    // ---------- Details panel ----------

    private void ShowDetail()
    {
        var t = Current;
        DetailEmpty.Visibility = t == null ? Visibility.Visible : Visibility.Collapsed;
        DetailBody.Visibility = t == null ? Visibility.Collapsed : Visibility.Visible;
        if (t == null) return;

        DetailName.Text = t.FullName;
        DetailBadge.Content = t.Status;

        InfoPanel.Children.Clear();
        AddLine("Contact", t.Contact ?? "-");
        AddLine("Emergency", t.EmergencyContact ?? "-");
        AddLine("School / work", t.SchoolOrWork ?? "-");
        AddLine("Current bed", t.CurrentBed);
        AddLine("Balance", Fmt.Peso(t.Balance), t.Overdue > 0 ? "Danger" : t.Balance > 0 ? "Warning" : "Success");
        if (t.Overdue > 0) AddLine("Overdue", Fmt.Peso(t.Overdue), "Danger");
        // Two of this tenant's stays on the same days: point it out so it can be corrected.
        var stayRanges = t.Stays.Select(s => new StayRange(s.Id, s.MoveIn, s.MoveOut)).ToList();
        if (stayRanges.Any(a => stayRanges.Any(b => a.Id < b.Id && StayRules.Overlaps(a, b))))
            AddLine("Check", "Two of this tenant's stays overlap. Use Edit this stay below to correct the dates.", "Danger");
        if (!string.IsNullOrWhiteSpace(t.Notes)) AddLine("Notes", t.Notes!);

        MoveInButton.IsEnabled = t.Status is "Moved out" or "No stay yet";
        MoveOutButton.IsEnabled = t.Status is "Staying" or "Reserved";
        MoveOutButton.Content = t.Status == "Reserved" ? "Cancel reservation" : "Move out";
        EditStayButton.IsEnabled = t.Stays.Count > 0;
        RefundDepositButton.IsEnabled = t.Stays.Any(s => s.Deposit > 0);
        ApplyDepositButton.IsEnabled = t.Stays.Any(s => s.Deposit > 0);

        HistoryPanel.Children.Clear();
        if (t.Stays.Count == 0)
        {
            HistoryPanel.Children.Add(new TextBlock
            {
                Text = "No stays recorded yet.",
                Style = (Style)FindResource("MutedText")
            });
            return;
        }

        foreach (var stay in t.Stays)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            box.Children.Add(new TextBlock
            {
                Text = stay.Bed,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("AppTextBrush")
            });
            var details = $"{Fmt.Date(stay.MoveIn)} to {(stay.MoveOut is DateOnly mo ? Fmt.Date(mo) : "present")}  -  {Fmt.Peso(stay.Rent)} / month";
            if (stay.Deposit > 0)
            {
                details += $"  -  Deposit {Fmt.Peso(stay.Deposit)}";
                if (stay.DepositRefunded > 0)
                    details += $" (refunded {Fmt.Peso(stay.DepositRefunded)}" +
                               (stay.DepositRefundedOn is DateOnly refundedOn ? $" on {Fmt.Date(refundedOn)}" : "") + ")";
            }
            box.Children.Add(new TextBlock
            {
                Text = details,
                Style = (Style)FindResource("MutedText")
            });

            var editLink = new Wpf.Ui.Controls.Button
            {
                Content = "Edit this stay",
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            editLink.Click += (_, _) => EditStay(stay.Id);
            box.Children.Add(editLink);

            HistoryPanel.Children.Add(box);
        }
    }

    private void AddLine(string label, string value, string? tone = null)
    {
        var line = new TextBlock { Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
        line.Inlines.Add(new Run(label + "   ") { Foreground = Res("AppSecondaryBrush") });
        line.Inlines.Add(new Run(value)
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = tone == null ? Res("AppTextBrush") : Res(tone + "Brush")
        });
        InfoPanel.Children.Add(line);
    }

    // ---------- Actions ----------

    private void AddTenant_Click(object sender, RoutedEventArgs e)
    {
        var v = FormDialog.Ask(Owner, "Add tenant", "Add tenant",
            new[]
            {
                ("Full name", ""),
                ("Contact number (optional)", ""),
                ("Emergency contact (optional)", ""),
                ("School or work (optional)", ""),
                ("Notes (optional)", "")
            },
            vals => NameError(vals[0], 0));
        if (v == null) return;

        using var db = new AppDbContext();
        var tenant = new Tenant
        {
            FullName = v[0].Trim(),
            Contact = Blank(v[1]),
            EmergencyContact = Blank(v[2]),
            SchoolOrWork = Blank(v[3]),
            Notes = Blank(v[4])
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        FilterCombo.SelectedIndex = 0;   // make sure the new tenant isn't hidden by a filter
        SearchBox.Text = "";
        Reload(tenant.Id);
    }

    private void EditTenant_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;

        var v = FormDialog.Ask(Owner, "Edit tenant", "Save changes",
            new[]
            {
                ("Full name", t.FullName),
                ("Contact number (optional)", t.Contact ?? ""),
                ("Emergency contact (optional)", t.EmergencyContact ?? ""),
                ("School or work (optional)", t.SchoolOrWork ?? ""),
                ("Notes (optional)", t.Notes ?? "")
            },
            vals => NameError(vals[0], t.Id));
        if (v == null) return;

        using var db = new AppDbContext();
        var entity = db.Tenants.Find(t.Id)!;
        entity.FullName = v[0].Trim();
        entity.Contact = Blank(v[1]);
        entity.EmergencyContact = Blank(v[2]);
        entity.SchoolOrWork = Blank(v[3]);
        entity.Notes = Blank(v[4]);
        db.SaveChanges();

        Reload(t.Id);
    }

    private void EditStay_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null || t.Stays.Count == 0) return;

        // The stay the tenant is in now (the one Move out acts on), otherwise their most recent one.
        var id = t.OccupancyId ?? t.Stays.OrderByDescending(s => s.MoveIn).First().Id;
        EditStay(id);
    }

    private void EditStay(int occupancyId)
    {
        var t = Current;
        if (t == null) return;

        using var db = new AppDbContext();
        var stay = db.Occupancies.Find(occupancyId);
        if (stay == null || stay.TenantId != t.Id) return;

        var chargeCount = db.Charges.IgnoreQueryFilters().Count(c => c.OccupancyId == stay.Id);

        DepositSummary depositSummary;
        using (var sumDb = new AppDbContext())
            depositSummary = DepositService.Summary(sumDb, stay.TenantId);
        var minDeposit = Math.Max(stay.DepositRefunded, stay.Deposit - depositSummary.Held);

        var beds = db.Beds
            .Include(b => b.Room)
            .ToList()
            .Where(b => b.Id == stay.BedId || (b.IsActive && b.Room.IsActive && !b.IsMaintenance))
            .OrderBy(b => b.Room.RoomName).ThenBy(b => b.BedLabel)
            .Select(b => new BedChoice { Id = b.Id, Display = $"{b.Room.RoomName} - {b.BedLabel}", Rate = b.MonthlyRate })
            .ToList();

        var current = new StayEdit
        {
            BedId = stay.BedId,
            MoveIn = stay.MoveInDate,
            MoveOut = stay.MoveOutDate,
            Rent = stay.AgreedRent,
            Deposit = stay.Deposit
        };

        var note = chargeCount > 0
            ? $"This stay has {chargeCount} billing record(s). Changing its dates does not change them yet. " +
              "Review them in Rent Billing and Billing Charges afterwards."
            : null;

        var result = StayEditDialog.Ask(Owner, $"Edit stay - {t.FullName}", note, beds, current,
            hardCheck: r => r.Deposit < minDeposit
                ? $"The deposit can't be less than {Fmt.Peso(minDeposit)}, the part already refunded or used for unpaid charges."
                : null,
            conflictText: r =>
            {
                using var check = new AppDbContext();
                return StayEditService.DescribeConflicts(check, stay, r.BedId, r.MoveIn, r.MoveOut);
            });
        if (result == null) return;

        var report = StayImpact.Analyze(db, stay, result);
        if (report.HasBillingImpact && !ImpactDialog.Confirm(Owner, t.FullName, report.ToText()))
            return;

        List<string> billingDone;
        try
        {
            StayEditService.Apply(db, stay, result);
            billingDone = RentRecalculation.Apply(db, report.RentPlan, stay, result.Reason);
            foreach (var billPlan in report.UtilityPlans)
                billingDone.AddRange(UtilityRecalculation.Apply(db, billPlan, result.Reason));
            db.SaveChanges();   // the stay, its billing corrections and the audit entries are saved together
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The stay could not be saved because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            return;
        }

        Reload(t.Id);

        if (billingDone.Count > 0)
            Tell("The stay was saved. Billing changes:\n\n" + string.Join("\n\n", billingDone));
    }

    private void StayHistory_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;
        AuditHistoryWindow.ShowFor(Owner, t.Id, t.FullName);
    }

    private void RefundDeposit_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;

        using var db = new AppDbContext();
        var stay = db.Occupancies
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .Where(o => o.TenantId == t.Id)
            .ToList()
            .Where(o => o.Deposit > 0)
            .OrderByDescending(o => o.MoveInDate)
            .FirstOrDefault();
        if (stay == null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        DepositSummary summary;
        using (var sumDb = new AppDbContext())
            summary = DepositService.Summary(sumDb, t.Id);
        var maxTotal = Math.Max(0, Math.Min(stay.Deposit,
            summary.Paid - (summary.Refunded - stay.DepositRefunded) - summary.Applied));

        string? Validate(string[] vals)
        {
            if (!TryMoney(vals[0], out var total))
                return "Enter the total refunded as a number (use 0 for none).";
            if (total > maxTotal)
                return $"The refund can't be more than {Fmt.Peso(maxTotal)}" +
                       (summary.Applied > 0 ? $" ({Fmt.Peso(summary.Applied)} of the deposit was used for unpaid charges)." : ".");
            if (total > 0)
            {
                if (!DateOnly.TryParse(vals[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                    return "Enter the refund date like 2026-10-14.";
                if (date > today)
                    return "The refund date can't be in the future.";
            }
            return null;
        }

        var note = $"{stay.Bed.Room.RoomName} / {stay.Bed.BedLabel}, stay from {Fmt.Date(stay.MoveInDate)}.\n" +
                   $"Deposit paid: {Fmt.Peso(stay.Deposit)}. Refunded so far: {Fmt.Peso(stay.DepositRefunded)}.\n" +
                   "Enter the TOTAL refunded so far. Enter 0 to remove a refund recorded by mistake.";

        var first = stay.DepositRefunded == 0 ? maxTotal : stay.DepositRefunded;

        var v = FormDialog.Ask(Owner, $"Deposit refund - {t.FullName}", "Save",
            new[]
            {
            ("Total refunded so far", first.ToString("0.##", CultureInfo.CurrentCulture)),
            ("Refund date (e.g. 2026-10-14)", (stay.DepositRefundedOn ?? today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("Note (optional)", stay.DepositRefundNote ?? "")
            },
            Validate, note);
        if (v == null) return;

        TryMoney(v[0], out var newTotal);
        newTotal = Math.Round(newTotal, 2);

        if (newTotal == 0)
        {
            stay.DepositRefunded = 0;
            stay.DepositRefundedOn = null;
            stay.DepositRefundNote = null;
        }
        else
        {
            DateOnly.TryParse(v[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var refundDate);
            stay.DepositRefunded = newTotal;
            stay.DepositRefundedOn = refundDate;
            stay.DepositRefundNote = string.IsNullOrWhiteSpace(v[2]) ? null : v[2].Trim();
        }

        db.SaveChanges();
        Reload(t.Id);
    }

    private void ApplyDeposit_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;

        DepositSummary summary;
        using (var db = new AppDbContext())
            summary = DepositService.Summary(db, t.Id);

        if (summary.Held <= 0)
        {
            Tell("There is no deposit left to use.");
            return;
        }
        if (t.Balance <= 0)
        {
            Tell("This tenant has no unpaid charges, so there is nothing to apply the deposit to.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var suggested = Math.Min(summary.Held, t.Balance);

        string? Validate(string[] vals)
        {
            if (!TryMoney(vals[0], out var amount) || amount <= 0)
                return "Enter the amount to apply as a number greater than zero.";
            if (amount > summary.Held)
                return $"That is more than the deposit still held ({Fmt.Peso(summary.Held)}).";
            if (amount > t.Balance)
                return $"That is more than the unpaid balance ({Fmt.Peso(t.Balance)}).";
            if (!DateOnly.TryParse(vals[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                return "Enter the date like 2026-10-14.";
            if (date > today)
                return "The date can't be in the future.";
            return null;
        }

        var note = $"Deposit still held: {Fmt.Peso(summary.Held)}. Unpaid balance: {Fmt.Peso(t.Balance)}.\n" +
                   "It is paid against the oldest unpaid charges first, and recorded as a payment with the method \"Deposit\".";

        var v = FormDialog.Ask(Owner, $"Apply deposit - {t.FullName}", "Apply",
            new[]
            {
            ("Amount to apply", suggested.ToString("0.##", CultureInfo.CurrentCulture)),
            ("Date (e.g. 2026-10-14)", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("Note (optional)", "")
            },
            Validate, note);
        if (v == null) return;

        TryMoney(v[0], out var applyAmount);
        DateOnly.TryParse(v[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var applyDate);
        var remarks = string.IsNullOrWhiteSpace(v[2]) ? "Deposit applied" : "Deposit applied - " + v[2].Trim();

        try
        {
            using var db = new AppDbContext();   // a fresh context, so the check runs on current data
            var error = DepositService.Apply(db, t.Id, Math.Round(applyAmount, 2), applyDate, remarks);
            if (error != null)
            {
                Tell(error);
                return;
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The deposit could not be applied because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            return;
        }

        Reload(t.Id);
    }

    private void MoveIn_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;

        using var db = new AppDbContext();

        var today = StayQueries.Today;

        // Beds with an ongoing (or open-ended reserved) stay can't take a stay with no end date,
        // but they stay in the list because a finished stay may still fit before it.
        var ongoing = db.Occupancies.Where(o => o.MoveOutDate == null).Select(o => o.BedId).ToList();

        var freeFrom = db.Occupancies
            .CurrentOrUpcoming(today)
            .Where(o => o.MoveOutDate != null)
            .Select(o => new { o.BedId, o.MoveOutDate })
            .ToList()
            .GroupBy(x => x.BedId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.MoveOutDate!.Value).AddDays(1));

        string Note(int bedId) =>
            ongoing.Contains(bedId) ? "  - occupied (no move-out date)"
            : freeFrom.TryGetValue(bedId, out var freeDay) ? $"  - free from {Fmt.Date(freeDay)}"
            : "";

        var beds = db.Beds
            .Include(b => b.Room)
            .Where(b => b.IsActive && b.Room.IsActive && !b.IsMaintenance)
            .ToList()
            .OrderBy(b => ongoing.Contains(b.Id) ? 2 : freeFrom.ContainsKey(b.Id) ? 1 : 0)
            .ThenBy(b => b.Room.RoomName).ThenBy(b => b.BedLabel)
            .Select(b => new BedChoice
            {
                Id = b.Id,
                Display = $"{b.Room.RoomName} - {b.BedLabel}  ({Fmt.Peso(b.MonthlyRate)}){Note(b.Id)}",
                Rate = b.MonthlyRate
            })
            .ToList();

        if (beds.Count == 0)
        {
            Tell("There are no beds to move into. Add a bed, or end maintenance on one, on the Rooms & Beds page.");
            return;
        }

        var result = StayDialog.AskMoveIn(Owner, t.FullName, beds, r =>
        {
            using var check = new AppDbContext();
            var proposed = new StayRange(0, r.Date, r.MoveOut);

            var bedConflicts = StayConflicts.ForBed(check, r.BedId, proposed);
            if (bedConflicts.Count > 0)
                return "That bed is not free for those dates. A move-out date is the tenant's last day, " +
                       "so the next tenant can move in the day after.\n" + StayConflicts.Describe(bedConflicts);

            var tenantConflicts = StayConflicts.ForTenant(check, t.Id, proposed);
            if (tenantConflicts.Count > 0)
                return "This tenant already has a stay on those dates.\n" + StayConflicts.Describe(tenantConflicts);

            return null;
        });
        if (result == null) return;

        var occupancy = new Occupancy
        {
            TenantId = t.Id,
            BedId = result.BedId,
            MoveInDate = result.Date,
            MoveOutDate = result.MoveOut,
            AgreedRent = result.Rent,
            Deposit = result.Deposit
        };
        db.Occupancies.Add(occupancy);
        db.SaveChanges();

        AuditService.Record(db, "Stay", occupancy.Id, t.Id, "Created", null,
                            StayEditService.Summary(db, occupancy), "New move-in");
        db.SaveChanges();

        Reload(t.Id);
    }

    private void MoveOut_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t?.OccupancyId == null || t.ActiveMoveIn == null) return;

        var today = StayQueries.Today;
        // A reservation has not started yet, so cancelling it removes it instead of ending a stay.
        if (t.ActiveMoveIn.Value > today)
        {
            CancelReservation(t);
            return;
        }

        // Every stay of this tenant that has started and has not ended before today.
        // Usually one. More only when overlapping stays were recorded.
        List<Occupancy> current;
        using (var load = new AppDbContext())
        {
            current = load.Occupancies.AsNoTracking()
                .Where(o => o.TenantId == t.Id && o.MoveInDate <= today
                            && (o.MoveOutDate == null || o.MoveOutDate >= today))
                .OrderBy(o => o.MoveInDate)
                .ToList();
        }
        if (current.Count == 0) return;

        var note = "The move-out date is the tenant's last day in the bed. You can pick today or a future date.";
        if (current.Count > 1)
            note += $"\nThis tenant has {current.Count} current stays. All of them end on the date you choose.";
        if (t.Balance > 0)
            note += $"\nHeads up: this tenant still has an unpaid balance of {Fmt.Peso(t.Balance)}.";

        var date = StayDialog.AskMoveOut(Owner, t.FullName, current.Max(o => o.MoveInDate), DateOnly.MaxValue, note);
        if (date == null) return;

        foreach (var stay in current)
            if (!EndStay(stay.Id, date.Value, t.FullName)) break;

        Reload(t.Id);
    }

    // Ends one stay on the given date. Returns false if the administrator should stop (cancelled or blocked).
    private bool EndStay(int occupancyId, DateOnly date, string tenantName)
    {
        using var db = new AppDbContext();
        var occupancy = db.Occupancies.Find(occupancyId);
        if (occupancy == null) return true;
        if (occupancy.MoveOutDate == date) return true;   // already ends on that day

        // Only a date that creates or lengthens an overlap is stopped. Ending a stay sooner, or ending one
        // that already overlaps another stay, is always allowed: it can only make an overlap smaller.
        var worse = StayConflicts.NewOnBed(db, occupancy, occupancy.BedId,
                                           new StayRange(occupancy.Id, occupancy.MoveInDate, date));
        if (worse.Count > 0)
        {
            var open = MessageBox.Show(
                "That move-out date would overlap another stay on this bed:\n" +
                StayConflicts.Describe(worse) +
                "\n\nOpen Edit stay for this stay, to record it as an exception?",
                "Move out", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (open == MessageBoxResult.Yes) EditStay(occupancy.Id);
            return false;
        }

        var proposedEdit = StayEditService.Snapshot(occupancy);
        proposedEdit.MoveOut = date;
        var report = StayImpact.Analyze(db, occupancy, proposedEdit);
        if (report.HasBillingImpact && !ImpactDialog.Confirm(Owner, tenantName, report.ToText()))
            return false;

        var before = occupancy.MoveOutDate;
        var moveOutReason = before == null ? "Move-out recorded" : "Move-out date changed";
        occupancy.MoveOutDate = date;
        AuditService.Record(db, "Stay", occupancy.Id, occupancy.TenantId, "MoveOutDate",
                            StayEditService.DateText(before), StayEditService.DateText(date),
                            moveOutReason);

        var billingDone = RentRecalculation.Apply(db, report.RentPlan, occupancy, moveOutReason);
        foreach (var billPlan in report.UtilityPlans)
            billingDone.AddRange(UtilityRecalculation.Apply(db, billPlan, moveOutReason));
        db.SaveChanges();

        if (billingDone.Count > 0)
            Tell("The move-out was saved. Billing changes:\n\n" + string.Join("\n\n", billingDone));
        return true;
    }

    private void CancelReservation(TenantRow t)
    {
        using var db = new AppDbContext();
        var occupancy = db.Occupancies.Find(t.OccupancyId!.Value)!;

        var message = $"Cancel the reservation of {t.FullName}?\n\n" +
                      $"They were due to move in on {Fmt.Date(occupancy.MoveInDate)}. " +
                      "The reservation will be removed and will not be billed.";

        if (occupancy.Deposit > 0)
            message += $"\n\nA deposit of {Fmt.Peso(occupancy.Deposit)} was recorded for this stay. " +
                       "Nexabode will not keep that record, so settle the deposit separately.";

        var confirm = MessageBox.Show(message, "Cancel reservation",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        // Billing records (even voided ones) point at the stay, so it can't be removed.
        var hasBilling = db.Charges.IgnoreQueryFilters().Any(c => c.OccupancyId == occupancy.Id);

        if (hasBilling)
        {
            AuditService.Record(db, "Stay", occupancy.Id, occupancy.TenantId, "MoveOutDate",
                                StayEditService.DateText(occupancy.MoveOutDate),
                                StayEditService.DateText(occupancy.MoveInDate),
                                "Reservation cancelled (billing records exist)");
            occupancy.MoveOutDate = occupancy.MoveInDate;
            db.SaveChanges();
            Tell("This reservation already has billing records, so it was kept and ended on its move-in date. " +
                 "Void its charges in Billing Charges so the tenant is not billed.");
        }
        else
        {
            AuditService.Record(db, "Stay", occupancy.Id, occupancy.TenantId, "Removed",
                                StayEditService.Summary(db, occupancy), null, "Reservation cancelled");
            db.Occupancies.Remove(occupancy);
            db.SaveChanges();
        }

        Reload(t.Id);
    }
}