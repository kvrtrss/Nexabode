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
        public List<string> SharedWith { get; set; } = new();   // other tenants booked on this bed on some of the same days
    }

    // A stay that was removed after it had been billed. Shown greyed in Stay history, nothing more.
    public class CancelledStayInfo
    {
        public string Bed { get; set; } = "";
        public DateOnly MoveIn { get; set; }
        public DateOnly? MoveOut { get; set; }
        public string Line { get; set; } = "";
    }

    public class TenantRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public bool HasClash { get; set; }   // a stay of this tenant clashes with another stay
        public string NameText => HasClash ? "\u26A0 " + FullName : FullName;
        public string? Gender { get; set; }
        public string GenderText => Gender ?? "-";
        public string? Contact { get; set; }
        public string? EmergencyContact { get; set; }
        public string? SchoolOrWork { get; set; }
        public string? Notes { get; set; }
        public string CurrentBed { get; set; } = "-";
        public string MoveInText { get; set; } = "-";
        public string Status { get; set; } = "";
        public decimal Balance { get; set; }
        public decimal Overdue { get; set; }
        public DepositSummary Credit { get; set; } = new(0, 0, 0);   // deposit paid, refunded, and used for charges
        public int? OccupancyId { get; set; }
        public DateOnly? ActiveMoveIn { get; set; }
        public DateOnly? LeavingOn { get; set; }   // the last day, when a move-out date is already set
        public List<StayInfo> Stays { get; set; } = new();
        public List<CancelledStayInfo> Cancelled { get; set; } = new();
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
        if (clean == "") return "Enter the tenant's full name.";

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

        // Cancelled stays are hidden from every query on purpose. They are read here only to be listed in Stay history.
        var cancelledStays = db.Occupancies.IgnoreQueryFilters()
            .Where(o => o.IsCancelled)
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

        // Credit already used to pay charges, per tenant. A voided payment is not counted, so its money is back in the credit.
        var creditUsed = db.Payments
            .Where(p => !p.IsVoided && p.Source == DepositService.PaymentSource)
            .ToList()
            .GroupBy(p => p.TenantId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        // Stays that still hold their bed (not ended, not moved out), for spotting two tenants on one bed.
        // This is the same set the Rooms & Beds page checks.
        var tenantNames = tenants.ToDictionary(t => t.Id, t => t.FullName);
        var holding = stays.Values
            .SelectMany(s => s)
            .Where(o => (o.MoveOutDate == null || o.MoveOutDate >= today) && o.MovedOutOn == null)
            .ToList();

        _all = tenants.Select(t =>
        {
            stays.TryGetValue(t.Id, out var list);
            list ??= new();
            charges.TryGetValue(t.Id, out var tenantCharges);
            tenantCharges ??= new();
            creditUsed.TryGetValue(t.Id, out var used);

            // A stay is current through its move-out date, unless the administrator already confirmed the tenant left.
            var currentStays = list
                .Where(o => (o.MoveOutDate == null || o.MoveOutDate >= today) && o.MovedOutOn == null)
                .OrderBy(o => o.MoveInDate)
                .ToList();
            var active = currentStays.FirstOrDefault();
            // Staying until the move-out date, "Moving out" on that day, "Moved out" from the day after
            // (or at once, when the administrator presses Move out).
            var status = active != null
                ? (active.MoveInDate > today ? "Reserved" : active.MoveOutDate == today ? "Moving out" : "Staying")
                : list.Count > 0 ? "Moved out" : "No stay yet";

            var balance = tenantCharges.Sum(c => c.Amount - c.Allocations.Sum(a => a.AmountApplied));
            var overdue = tenantCharges
                .Where(c => c.DueDate < today)
                .Sum(c => Math.Max(0, c.Amount - c.Allocations.Sum(a => a.AmountApplied)));

            static string BedName(Occupancy o) => $"{o.Bed.Room.RoomName} / {o.Bed.BedLabel}";

            // Other tenants booked on the same bed on some of the same days as this stay.
            List<string> SharedWith(Occupancy o) => (o.MoveOutDate == null || o.MoveOutDate >= today) && o.MovedOutOn == null
                ? holding
                    .Where(x => x.TenantId != o.TenantId && x.BedId == o.BedId &&
                                StayRules.Overlap(o.BedRange(), x.BedRange()) != null)
                    .Select(x => tenantNames[x.TenantId])
                    .Distinct().OrderBy(n => n).ToList()
                : new List<string>();

            var ownOverlap = list.Any(a => list.Any(b => a.Id < b.Id &&
                StayRules.Overlaps(new StayRange(a.Id, a.MoveInDate, a.MoveOutDate), new StayRange(b.Id, b.MoveInDate, b.MoveOutDate))));

            var stayInfos = list.Select(o => new StayInfo
            {
                Id = o.Id,
                Bed = BedName(o),
                MoveIn = o.MoveInDate,
                MoveOut = o.MoveOutDate,
                Rent = o.AgreedRent,
                Deposit = o.Deposit,
                DepositRefunded = o.DepositRefunded,
                DepositRefundedOn = o.DepositRefundedOn,
                SharedWith = SharedWith(o)
            }).ToList();

            return new TenantRow
            {
                Id = t.Id,
                FullName = t.FullName,
                HasClash = ownOverlap || stayInfos.Any(s => s.SharedWith.Count > 0),
                Gender = GenderRules.Normalize(t.Gender),
                Contact = t.Contact,
                EmergencyContact = t.EmergencyContact,
                SchoolOrWork = t.SchoolOrWork,
                Notes = t.Notes,
                Status = status,
                Balance = balance,
                Overdue = overdue,
                Credit = new DepositSummary(list.Sum(o => o.Deposit), list.Sum(o => o.DepositRefunded), used),
                CurrentBed = active != null
                    ? BedName(active)
                      + (active.MoveOutDate is DateOnly until ? $" (until {Fmt.Date(until)})" : "")
                      + (currentStays.Count > 1 ? $"  +{currentStays.Count - 1} more stay(s)" : "")
                    : "-",
                MoveInText = active != null ? Fmt.Date(active.MoveInDate) : "-",
                OccupancyId = active?.Id,
                ActiveMoveIn = active?.MoveInDate,
                LeavingOn = status == "Moving out" ? active!.MoveOutDate : null,
                Stays = stayInfos,
                Cancelled = cancelledStays.TryGetValue(t.Id, out var cancelledList)
                    ? cancelledList.Select(o => new CancelledStayInfo
                    {
                        Bed = BedName(o),
                        MoveIn = o.MoveInDate,
                        MoveOut = o.MoveOutDate,
                        Line = StayCancelRules.HistoryLine(o.CancelledOn, o.CancelReason)
                    }).ToList()
                    : new List<CancelledStayInfo>()
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

        StayingCard.Value = _all.Count(r => r.Status is "Staying" or "Moving out").ToString();
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
                "Staying" => r.Status is "Staying" or "Moving out",
                "Moving out" => r.Status == "Moving out",
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

    // A moved-out tenant with nothing owed and no credit left is "archived": hidden from the default list, never deleted.
    private static bool IsArchived(TenantRow r) => r.Status == "Moved out" && r.Balance <= 0 && r.Credit.Held <= 0;

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
        AddLine("Gender", GenderRules.Display(t.Gender));
        AddLine("Contact", t.Contact ?? "-");
        AddLine("Emergency", t.EmergencyContact ?? "-");
        AddLine("School / work", t.SchoolOrWork ?? "-");
        AddLine("Current bed", t.CurrentBed);
        if (t.LeavingOn is DateOnly leavingOn) AddLine("Last day", Fmt.Date(leavingOn), "Warning");
        AddLine("Balance", Fmt.Peso(t.Balance), t.Overdue > 0 ? "Danger" : t.Balance > 0 ? "Warning" : "Success");
        if (t.Overdue > 0) AddLine("Overdue", Fmt.Peso(t.Overdue), "Danger");
        AddLine("Credit balance", Fmt.Peso(t.Credit.Held), t.Credit.Held > 0 ? "Success" : null);
        if (t.Credit.Applied > 0) AddLine("Credit used", Fmt.Peso(t.Credit.Applied));
        if (t.Credit.Refunded > 0) AddLine("Credit refunded", Fmt.Peso(t.Credit.Refunded));
        // Two of this tenant's stays on the same days: point it out so it can be corrected or removed.
        var overlapping = t.Stays
            .SelectMany(a => t.Stays
                .Where(b => a.Id < b.Id &&
                            StayRules.Overlaps(new StayRange(a.Id, a.MoveIn, a.MoveOut), new StayRange(b.Id, b.MoveIn, b.MoveOut)))
                .Select(b => (A: a, B: b)))
            .ToList();
        if (overlapping.Count > 0)
        {
            var duplicate = overlapping.Any(p => StayRemovalRules.IsDuplicate(
                p.A.Bed, p.A.MoveIn, p.A.MoveOut, p.B.Bed, p.B.MoveIn, p.B.MoveOut));
            AddLine("Check", StayRemovalRules.OverlapWarning(duplicate, Session.IsOwner), "Danger");
        }

        // Someone else is booked on the same bed on some of the same days.
        foreach (var bed in t.Stays.Where(s => s.SharedWith.Count > 0).GroupBy(s => s.Bed))
        {
            var others = bed.SelectMany(s => s.SharedWith).Distinct().OrderBy(n => n).ToList();
            AddLine("Check", ClashWarnings.TenantBedWarning(bed.Key, others, Session.IsOwner), "Danger");
        }
        if (!string.IsNullOrWhiteSpace(t.Notes)) AddLine("Notes", t.Notes!);

        MoveInButton.IsEnabled = t.Status is "Moved out" or "No stay yet";
        MoveOutButton.IsEnabled = t.Status is "Staying" or "Moving out" or "Reserved";
        MoveOutButton.Content = t.Status == "Reserved" ? "Cancel reservation" : "Move out";
        EditStayButton.IsEnabled = t.Stays.Count > 0;
        RefundDepositButton.IsEnabled = t.Credit.Held > 0;
        ApplyDepositButton.IsEnabled = t.Credit.Held > 0;
        EditCreditButton.IsEnabled = t.Stays.Count > 0;           // credit is kept on a stay
        UndoRefundButton.IsEnabled = t.Credit.Refunded > 0;

        HistoryPanel.Children.Clear();
        if (t.Stays.Count == 0 && t.Cancelled.Count == 0)
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

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

            var editLink = new Wpf.Ui.Controls.Button { Content = "Edit this stay" };
            editLink.Click += (_, _) => EditStay(stay.Id);
            actions.Children.Add(editLink);

            // Only an Owner can remove a stay, and only one that was never billed.
            if (Session.IsOwner)
            {
                var removeLink = new Wpf.Ui.Controls.Button
                {
                    Content = "Remove this stay",
                    Style = (Style)Application.Current.FindResource("DangerButton"),
                    Margin = new Thickness(8, 0, 0, 0),
                    ToolTip = "For a stay entered by mistake. Deleted if it was never billed, otherwise kept as Cancelled."
                };
                removeLink.Click += (_, _) => RemoveStay(stay.Id);
                actions.Children.Add(removeLink);
            }

            box.Children.Add(actions);

            HistoryPanel.Children.Add(box);
        }

        // Stays that were removed after being billed: kept for the records, greyed, with no buttons.
        foreach (var cancelled in t.Cancelled)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 8), Opacity = 0.7 };
            box.Children.Add(new TextBlock
            {
                Text = cancelled.Bed,
                FontWeight = FontWeights.SemiBold,
                TextDecorations = TextDecorations.Strikethrough,
                Foreground = Res("AppSecondaryBrush")
            });
            box.Children.Add(new TextBlock
            {
                Text = $"{Fmt.Date(cancelled.MoveIn)} to {(cancelled.MoveOut is DateOnly mo ? Fmt.Date(mo) : "present")}",
                Style = (Style)FindResource("MutedText")
            });
            box.Children.Add(new TextBlock
            {
                Text = cancelled.Line,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)FindResource("MutedText")
            });
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
                ("Gender (optional)", GenderRules.NotSet),
                ("Contact number (optional)", ""),
                ("Emergency contact (optional)", ""),
                ("School or work (optional)", ""),
                ("Notes (optional)", "")
            },
            vals => NameError(vals[0], 0) ?? GenderRules.Error(vals[1]),
            choices: new string[]?[] { null, GenderRules.Choices, null, null, null, null });
        if (v == null) return;

        using var db = new AppDbContext();
        var tenant = new Tenant
        {
            FullName = v[0].Trim(),
            Gender = GenderRules.Normalize(v[1]),
            Contact = Blank(v[2]),
            EmergencyContact = Blank(v[3]),
            SchoolOrWork = Blank(v[4]),
            Notes = Blank(v[5])
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
                ("Gender (optional)", GenderRules.Display(t.Gender)),
                ("Contact number (optional)", t.Contact ?? ""),
                ("Emergency contact (optional)", t.EmergencyContact ?? ""),
                ("School or work (optional)", t.SchoolOrWork ?? ""),
                ("Notes (optional)", t.Notes ?? "")
            },
            vals => NameError(vals[0], t.Id) ?? GenderRules.Error(vals[1]),
            choices: new string[]?[] { null, GenderRules.Choices, null, null, null, null });
        if (v == null) return;

        // Show what is about to change. If nothing did, there is nothing to save.
        var changes = ConfirmWording.Changes(
            ("Full name", t.FullName, v[0]),
            ("Gender", GenderRules.Display(t.Gender), GenderRules.Display(v[1])),
            ("Contact number", t.Contact, v[2]),
            ("Emergency contact", t.EmergencyContact, v[3]),
            ("School or work", t.SchoolOrWork, v[4]),
            ("Notes", t.Notes, v[5]));
        if (changes.Count == 0) return;
        if (!ConfirmActionDialog.Ask(Owner, "Edit tenant", ConfirmWording.EditTenant(t.FullName, changes))) return;

        using var db = new AppDbContext();
        var entity = db.Tenants.Find(t.Id)!;
        entity.FullName = v[0].Trim();
        entity.Gender = GenderRules.Normalize(v[1]);
        entity.Contact = Blank(v[2]);
        entity.EmergencyContact = Blank(v[3]);
        entity.SchoolOrWork = Blank(v[4]);
        entity.Notes = Blank(v[5]);
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
        if (t == null || t.Stays.All(s => s.Id != occupancyId)) return;

        StayActions.Edit(Owner, occupancyId, () => Reload(t.Id));
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

        DepositSummary summary;
        using (var db = new AppDbContext())
            summary = DepositService.Summary(db, t.Id);

        if (summary.Held <= 0)
        {
            Tell("There is no credit left to refund.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        string? Validate(string[] vals)
        {
            if (!TryMoney(vals[0], out var amount) || Math.Round(amount, 2) <= 0)
                return "Enter the amount to refund as a number greater than zero.";
            if (Math.Round(amount, 2) > summary.Held)
                return $"That is more than the credit still held ({Fmt.Peso(summary.Held)}).";
            if (!DateOnly.TryParse(vals[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                return "Enter the refund date like 2026-10-14.";
            if (date > today)
                return "The refund date can't be in the future.";
            return null;
        }

        var note = $"Credit still held: {Fmt.Peso(summary.Held)}. Only the credit that is left can be refunded.\n" +
                   "Credit that was already used for charges, or already refunded, can't be refunded again.";

        var v = FormDialog.Ask(Owner, $"Refund credit: {t.FullName}", "Refund",
            new[]
            {
            ("Amount to refund", summary.Held.ToString("0.##", CultureInfo.CurrentCulture)),
            ("Refund date (e.g. 2026-10-14)", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("Note (optional)", "")
            },
            Validate, note);
        if (v == null) return;

        TryMoney(v[0], out var refundAmount);
        refundAmount = Math.Round(refundAmount, 2);
        DateOnly.TryParse(v[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var refundDate);
        var refundNote = string.IsNullOrWhiteSpace(v[2]) ? null : v[2].Trim();

        if (!ConfirmActionDialog.Ask(Owner, "Refund credit",
                ConfirmWording.RefundCredit(t.FullName, refundAmount, summary.Held), "Refund")) return;

        try
        {
            using var db = new AppDbContext();   // a fresh context, so the check runs on current data
            var error = DepositService.Refund(db, t.Id, refundAmount, refundDate, refundNote);
            if (error != null)
            {
                Tell(error);
                Reload(t.Id);
                return;
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The refund could not be saved because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            return;
        }

        Reload(t.Id);
    }

    private void EditCredit_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;

        DepositSummary summary;
        using (var db = new AppDbContext())
            summary = DepositService.Summary(db, t.Id);

        string? Validate(string[] vals)
        {
            if (!TryMoney(vals[0], out var amount) || Math.Round(amount, 2) < 0)
                return "Enter the credit as a number, zero or more.";
            if (Math.Round(amount, 2) == summary.Held)
                return "That is the same as the current credit. Enter a different amount.";
            if (!ReasonRules.IsReal(vals[1]))
                return "Please enter a reason (at least 3 letters or digits).";
            return null;
        }

        var note = $"Current credit: {Fmt.Peso(summary.Held)} (deposit {Fmt.Peso(summary.Paid)}, " +
                   $"used {Fmt.Peso(summary.Applied)}, refunded {Fmt.Peso(summary.Refunded)}).\n" +
                   "Use this to correct a credit that was entered wrongly. The change is saved in Change history. " +
                   "To give money back to the tenant, use Refund credit instead.";

        var v = FormDialog.Ask(Owner, $"Edit credit: {t.FullName}", "Save changes",
            new[]
            {
            ("New credit balance", summary.Held.ToString("0.##", CultureInfo.CurrentCulture)),
            ("Reason for the change", "")
            },
            Validate, note);
        if (v == null) return;

        TryMoney(v[0], out var newCredit);

        if (!ConfirmActionDialog.Ask(Owner, "Edit credit",
                ConfirmWording.EditCredit(t.FullName, summary.Held, Math.Round(newCredit, 2), v[1]), "Save credit")) return;

        try
        {
            using var db = new AppDbContext();   // a fresh context, so the check runs on current data
            var error = DepositService.SetCredit(db, t.Id, newCredit, v[1]);
            if (error != null)
            {
                Tell(error);
                Reload(t.Id);
                return;
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The credit could not be saved because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            return;
        }

        Reload(t.Id);
    }

    private void UndoRefund_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;

        DepositSummary summary;
        using (var db = new AppDbContext())
            summary = DepositService.Summary(db, t.Id);

        if (summary.Refunded <= 0)
        {
            Tell("No credit has been refunded, so there is nothing to undo.");
            return;
        }

        string? Validate(string[] vals)
        {
            if (!TryMoney(vals[0], out var amount) || Math.Round(amount, 2) <= 0)
                return "Enter the amount to put back as a number greater than zero.";
            if (Math.Round(amount, 2) > summary.Refunded)
                return $"That is more than what was refunded ({Fmt.Peso(summary.Refunded)}).";
            if (!ReasonRules.IsReal(vals[1]))
                return "Please enter a reason (at least 3 letters or digits).";
            return null;
        }

        var note = $"Refunded so far: {Fmt.Peso(summary.Refunded)}. Use this only if a refund was recorded by mistake " +
                   "or the money was not really handed back. The amount goes back into the tenant's credit, " +
                   "and the change is saved in Change history.";

        var v = FormDialog.Ask(Owner, $"Undo refund: {t.FullName}", "Undo refund",
            new[]
            {
            ("Amount to put back into credit", summary.Refunded.ToString("0.##", CultureInfo.CurrentCulture)),
            ("Reason", "")
            },
            Validate, note);
        if (v == null) return;

        TryMoney(v[0], out var putBack);
        putBack = Math.Round(putBack, 2);

        if (!ConfirmActionDialog.Ask(Owner, "Undo refund",
                ConfirmWording.UndoRefund(t.FullName, putBack, summary.Held), "Undo refund")) return;

        try
        {
            using var db = new AppDbContext();   // a fresh context, so the check runs on current data
            var error = DepositService.UndoRefund(db, t.Id, putBack, v[1]);
            if (error != null)
            {
                Tell(error);
                Reload(t.Id);
                return;
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Tell("The refund could not be undone because the data changed at the same moment. " +
                 "Nothing was saved. Please try again.");
            return;
        }

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
            Tell("There is no credit left to use.");
            return;
        }
        if (t.Balance <= 0)
        {
            Tell("This tenant has no unpaid charges, so there is nothing to apply the credit to.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var suggested = Math.Min(summary.Held, t.Balance);

        string? Validate(string[] vals)
        {
            if (!TryMoney(vals[0], out var amount) || amount <= 0)
                return "Enter the amount to apply as a number greater than zero.";
            if (amount > summary.Held)
                return $"That is more than the credit still held ({Fmt.Peso(summary.Held)}).";
            if (amount > t.Balance)
                return $"That is more than the unpaid balance ({Fmt.Peso(t.Balance)}).";
            if (!DateOnly.TryParse(vals[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                return "Enter the date like 2026-10-14.";
            if (date > today)
                return "The date can't be in the future.";
            return null;
        }

        var note = $"Credit still held: {Fmt.Peso(summary.Held)}. Unpaid balance: {Fmt.Peso(t.Balance)}.\n" +
                   "It is paid against the oldest unpaid charges first, and recorded as a payment with the method \"Credit\".";

        var v = FormDialog.Ask(Owner, $"Apply credit: {t.FullName}", "Apply",
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
        var remarks = string.IsNullOrWhiteSpace(v[2]) ? "Credit applied" : "Credit applied - " + v[2].Trim();

        if (!ConfirmActionDialog.Ask(Owner, "Apply credit",
                ConfirmWording.ApplyCredit(t.FullName, Math.Round(applyAmount, 2), summary.Held, t.Balance), "Apply credit")) return;

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
            Tell("The credit could not be applied because the data changed at the same moment. " +
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
                return "This move-in cannot be saved because the bed is already taken for part of that period. Choose different dates or a different bed. " +
                       "A move-out date is the tenant's last day, so the next tenant can move in the day after.\n\n" + StayConflicts.Describe(bedConflicts);

            var tenantConflicts = StayConflicts.ForTenant(check, t.Id, proposed);
            if (tenantConflicts.Count > 0)
                return "This move-in cannot be saved because the tenant already has a stay during that period. Choose different dates.\n\n" + StayConflicts.Describe(tenantConflicts);

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

        var note = "Choose today to move the tenant out now, or a later date to schedule the move-out. " +
                   "The move-out date is their last day in the bed, and it is still billed. " +
                   "A scheduled tenant stays listed as Staying until that day, is Moving out on that day, and is Moved out the day after.";
        var planned = current.Max(o => o.MoveOutDate);
        if (planned != null && planned.Value > today)
            note += $"\nA move-out date is already set: {Fmt.Date(planned.Value)}. Choosing today moves them out now instead.";
        if (current.Count > 1)
            note += $"\nThis tenant has {current.Count} current stays. All of them will end on the date you choose.";
        if (t.Balance > 0)
            note += $"\nNote: this tenant still has an unpaid balance of {Fmt.Peso(t.Balance)}.";

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
        var today = StayQueries.Today;
        if (occupancy.MoveOutDate == date)
        {
            // The date is not changing. If it is today or earlier, pressing Move out confirms the tenant has left.
            if (date <= today && occupancy.MovedOutOn == null)
            {
                occupancy.MovedOutOn = today;
                AuditService.Record(db, "Stay", occupancy.Id, occupancy.TenantId, "MovedOut",
                                    null, StayEditService.DateText(today), "Move-out confirmed");
                db.SaveChanges();
            }
            return true;   // already ends on that day
        }

        // Only a date that creates or lengthens an overlap is stopped. Ending a stay sooner, or ending one
        // that already overlaps another stay, is always allowed: it can only make an overlap smaller.
        var worse = StayConflicts.NewOnBed(db, occupancy, occupancy.BedId,
                                           new StayRange(occupancy.Id, occupancy.MoveInDate, date));
        if (worse.Count > 0)
        {
            var open = ConfirmActionDialog.YesNo(
                "This move-out date cannot be applied because it would overlap another stay on the same bed.\n\n" +
                StayConflicts.Describe(worse) +
                "\n\nOpen Edit stay, where the overlap can be recorded as an exception?",
                "Move out", confirmText: "Open Edit stay");
            if (open == MessageBoxResult.Yes) EditStay(occupancy.Id);
            return false;
        }

        var proposedEdit = StayEditService.Snapshot(occupancy);
        proposedEdit.MoveOut = date;
        var report = StayImpact.Analyze(db, occupancy, proposedEdit);
        if (report.HasBillingImpact && !ImpactDialog.Confirm(Owner, tenantName, report.ToText(), "Save move-out"))
            return false;

        var before = occupancy.MoveOutDate;
        var moveOutReason = before == null ? "Move-out recorded" : "Move-out date changed";
        occupancy.MoveOutDate = date;
        AuditService.Record(db, "Stay", occupancy.Id, occupancy.TenantId, "MoveOutDate",
                            StayEditService.DateText(before), StayEditService.DateText(date),
                            moveOutReason);

        // A move-out dated today or earlier means the tenant has left, so they are Moved out at once.
        // A later date is only a schedule: they stay listed as Staying until that day.
        occupancy.MovedOutOn = date <= today ? today : null;
        if (occupancy.MovedOutOn != null)
            AuditService.Record(db, "Stay", occupancy.Id, occupancy.TenantId, "MovedOut",
                                null, StayEditService.DateText(today), "Move-out confirmed");

        var billingDone = RentRecalculation.Apply(db, report.RentPlan, occupancy, moveOutReason);
        billingDone.AddRange(RentRestore.VoidedMonthNotes(db, occupancy, new StayRange(occupancy.Id, occupancy.MoveInDate, before)));
        foreach (var billPlan in report.UtilityPlans)
            billingDone.AddRange(UtilityRecalculation.Apply(db, billPlan, moveOutReason));
        db.SaveChanges();

        if (billingDone.Count > 0)
            Tell("The move-out was saved. Billing changes and notes:\n\n" + string.Join("\n\n", billingDone));
        return true;
    }

    // Removes a stay that was entered by mistake. The rules live in StayActions, shared with Rooms & Beds.
    private void RemoveStay(int stayId)
    {
        var t = Current;
        if (t == null || t.Stays.All(s => s.Id != stayId)) return;

        StayActions.Remove(Owner, stayId, () => Reload(t.Id));
    }

    private void CancelReservation(TenantRow t)
    {
        using var db = new AppDbContext();
        var occupancy = db.Occupancies.Find(t.OccupancyId!.Value)!;

        var message = $"Cancel the reservation of {t.FullName}?\n\n" +
                      $"They were due to move in on {Fmt.Date(occupancy.MoveInDate)}. " +
                      "The reservation will be removed and will not be billed. This cannot be undone.";

        if (occupancy.Deposit > 0)
            message += $"\n\nA deposit of {Fmt.Peso(occupancy.Deposit)} was recorded for this stay. " +
                       "Nexabode will not keep that record, so settle the deposit separately.";

        var confirm = ConfirmActionDialog.YesNo(message, "Cancel reservation", confirmText: "Cancel reservation");
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