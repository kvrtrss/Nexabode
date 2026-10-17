using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class TenantsPage : Page
{
    public class StayInfo
    {
        public string Bed { get; set; } = "";
        public DateOnly MoveIn { get; set; }
        public DateOnly? MoveOut { get; set; }
        public decimal Rent { get; set; }
        public decimal Deposit { get; set; }
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

            var active = list.FirstOrDefault(o => o.MoveOutDate == null);
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
                CurrentBed = active != null ? BedName(active) : "-",
                MoveInText = active != null ? Fmt.Date(active.MoveInDate) : "-",
                OccupancyId = active?.Id,
                ActiveMoveIn = active?.MoveInDate,
                Stays = list.Select(o => new StayInfo
                {
                    Bed = BedName(o),
                    MoveIn = o.MoveInDate,
                    MoveOut = o.MoveOutDate,
                    Rent = o.AgreedRent,
                    Deposit = o.Deposit
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
        if (!string.IsNullOrWhiteSpace(t.Notes)) AddLine("Notes", t.Notes!);

        MoveInButton.IsEnabled = t.Status is "Moved out" or "No stay yet";
        MoveOutButton.IsEnabled = t.Status is "Staying" or "Reserved";
        MoveOutButton.Content = t.Status == "Reserved" ? "Cancel reservation" : "Move out";

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
            if (stay.Deposit > 0) details += $"  -  Deposit {Fmt.Peso(stay.Deposit)}";
            box.Children.Add(new TextBlock
            {
                Text = details,
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

    private void MoveIn_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t == null) return;

        using var db = new AppDbContext();

        var taken = db.Occupancies.Where(o => o.MoveOutDate == null).Select(o => o.BedId).ToList();
        var beds = db.Beds
            .Include(b => b.Room)
            .Where(b => b.IsActive && b.Room.IsActive && !b.IsMaintenance)
            .ToList()
            .Where(b => !taken.Contains(b.Id))
            .OrderBy(b => b.Room.RoomName).ThenBy(b => b.BedLabel)
            .Select(b => new BedChoice
            {
                Id = b.Id,
                Display = $"{b.Room.RoomName} - {b.BedLabel}  ({Fmt.Peso(b.MonthlyRate)})",
                Rate = b.MonthlyRate
            })
            .ToList();

        if (beds.Count == 0)
        {
            Tell("There are no available beds right now. Add a bed, or end maintenance on one, on the Rooms & Beds page.");
            return;
        }

        var previousOuts = t.Stays.Where(s => s.MoveOut != null).Select(s => s.MoveOut!.Value).ToList();
        DateOnly? lastOut = previousOuts.Count > 0 ? previousOuts.Max() : null;

        var result = StayDialog.AskMoveIn(Owner, t.FullName, beds, r =>
        {
            using var check = new AppDbContext();
            if (check.Occupancies.Any(o => o.BedId == r.BedId && o.MoveOutDate == null))
                return "That bed was just taken. Please choose another.";
            if (lastOut != null && r.Date < lastOut)
                return $"The move-in date can't be before this tenant's previous move-out ({Fmt.Date(lastOut.Value)}).";
            // The bed's earlier occupants must be gone before the new stay starts.
            var bedOuts = check.Occupancies
                .Where(o => o.BedId == r.BedId && o.MoveOutDate != null)
                .Select(o => o.MoveOutDate)
                .ToList();
            if (bedOuts.Count > 0)
            {
                var bedLastOut = bedOuts.Max()!.Value;
                if (r.Date < bedLastOut)
                    return $"That bed was occupied until {Fmt.Date(bedLastOut)}. The move-in date can't be before that.";
            }
            return null;
        });
        if (result == null) return;

        db.Occupancies.Add(new Occupancy
        {
            TenantId = t.Id,
            BedId = result.BedId,
            MoveInDate = result.Date,
            AgreedRent = result.Rent,
            Deposit = result.Deposit
        });
        db.SaveChanges();

        Reload(t.Id);
    }

    private void MoveOut_Click(object sender, RoutedEventArgs e)
    {
        var t = Current;
        if (t?.OccupancyId == null || t.ActiveMoveIn == null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        // A reservation has not started yet, so cancelling it removes it instead of ending a stay.
        if (t.ActiveMoveIn.Value > today)
        {
            CancelReservation(t);
            return;
        }
        var min = t.ActiveMoveIn.Value;
        var max = today > min ? today : min;   // a reserved stay can only be cancelled on its move-in date

        var note = t.Balance > 0
            ? $"Heads up: this tenant still has an unpaid balance of {Fmt.Peso(t.Balance)}."
            : null;

        var date = StayDialog.AskMoveOut(Owner, t.FullName, min, max, note);
        if (date == null) return;

        using var db = new AppDbContext();
        var occupancy = db.Occupancies.Find(t.OccupancyId.Value)!;
        occupancy.MoveOutDate = date;
        db.SaveChanges();

        Reload(t.Id);
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
            occupancy.MoveOutDate = occupancy.MoveInDate;
            db.SaveChanges();
            Tell("This reservation already has billing records, so it was kept and ended on its move-in date. " +
                 "Void its charges in Billing Charges so the tenant is not billed.");
        }
        else
        {
            db.Occupancies.Remove(occupancy);
            db.SaveChanges();
        }

        Reload(t.Id);
    }
}   