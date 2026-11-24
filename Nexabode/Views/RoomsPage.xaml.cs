using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class RoomsPage : Page
{
    public class OccupantInfo
    {
        public string Name { get; set; } = "";
        public string? Gender { get; set; }
        public string BedLabel { get; set; } = "";
        public DateOnly MoveIn { get; set; }
        public bool IsReserved { get; set; }
        public DateOnly? MoveOut { get; set; }
        public string OverlapNote { get; set; } = "";
        public int StayId { get; set; }
    }

    public class BedInfo
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public decimal Rate { get; set; }
        public bool IsActive { get; set; }
        public bool HasOverlap => Occupants.Any(o => o.OverlapNote != "");
        public string Status { get; set; } = "";
        public List<OccupantInfo> Occupants { get; set; } = new();
    }

    public class RoomInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Floor { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public List<BedInfo> Beds { get; set; } = new();
    }

    private List<RoomInfo> _rooms = new();
    private int? _roomId;
    private int? _bedId;
    private string _filter = "All";

    public RoomsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    // ---------- Helpers ----------


    // Windows opened from the details window belong to it, so they appear above it.
    private Window? Owner => (Window?)_details ?? Window.GetWindow(this);

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value >= 0;

    private static string ToneOf(string status) => status switch
    {
        "Available" => "Success",
        "Occupied" => "Occupied",
        "Reserved" => "Warning",
        "Maintenance" => "Info",
        _ => "Neutral"
    };

    private static string StatusOf(Room room, Bed bed, DateOnly? moveIn, DateOnly today)
    {
        if (!room.IsActive || !bed.IsActive) return "Inactive";
        if (bed.IsMaintenance) return "Maintenance";
        if (moveIn == null) return "Available";
        return moveIn > today ? "Reserved" : "Occupied";
    }

    private RoomInfo? CurrentRoom => _rooms.FirstOrDefault(r => r.Id == _roomId);
    private BedInfo? SelectedBed => CurrentRoom?.Beds.FirstOrDefault(b => b.Id == _bedId);

    internal void Reload(int? selectRoom = null)
    {
        if (selectRoom != null && selectRoom != _roomId)
        {
            _roomId = selectRoom;
            _bedId = null;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = AppDb.Create();

        var openStays = db.Occupancies
            .Include(o => o.Tenant)
            .CurrentOrUpcoming(today)
            .ToList();

        var moveIns = openStays
            .GroupBy(o => o.BedId)
            .ToDictionary(g => g.Key, g => g.Min(o => o.MoveInDate));

        var staysByBed = openStays
            .GroupBy(o => o.BedId)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.MoveInDate).ToList());

        _rooms = db.Rooms
            .Include(r => r.Beds)
            .OrderBy(r => r.RoomName)
            .ToList()
            .Select(r => new RoomInfo
            {
                Id = r.Id,
                Name = r.RoomName,
                Floor = FloorRules.Normalize(r.Floor),   // older rooms may have it typed differently or not at all
                Notes = r.Notes,
                IsActive = r.IsActive,
                Beds = r.Beds
                    .OrderBy(b => b.BedLabel)
                    .Select(b => new BedInfo
                    {
                        Id = b.Id,
                        Label = b.BedLabel,
                        Rate = b.MonthlyRate,
                        IsActive = b.IsActive,
                        Status = StatusOf(r, b, MoveInOf(moveIns, b.Id), today),
                        Occupants = OccupantsOf(staysByBed, b, today)
                    })
                    .ToList()
            })
            .ToList();

        if (_roomId != null && CurrentRoom == null) _roomId = null;

        Render();
    }

    private static DateOnly? MoveInOf(Dictionary<int, DateOnly> map, int bedId) =>
        map.TryGetValue(bedId, out var date) ? date : null;

    private static string SpanText(DateSpan s) =>
        s.End is DateOnly e
            ? (e == s.Start ? Fmt.Date(s.Start) : $"{Fmt.Date(s.Start)} to {Fmt.Date(e)}")
            : $"{Fmt.Date(s.Start)} onward";

    private static List<OccupantInfo> OccupantsOf(Dictionary<int, List<Occupancy>> map, Bed bed, DateOnly today)
    {
        if (!map.TryGetValue(bed.Id, out var stays)) return new List<OccupantInfo>();

        return stays.Select(o =>
        {
            var range = new StayRange(o.Id, o.MoveInDate, o.MoveOutDate);

            // Other stays on this bed that share days with this one.
            var clashes = stays
                .Where(other => other.Id != o.Id)
                .Select(other => (Other: other,
                                  Span: StayRules.Overlap(range, new StayRange(other.Id, other.MoveInDate, other.MoveOutDate))))
                .Where(x => x.Span != null)
                .Select(x => $"{x.Other.Tenant.FullName} ({SpanText(x.Span!.Value)})")
                .ToList();

            return new OccupantInfo
            {
                StayId = o.Id,
                Name = o.Tenant.FullName,
                Gender = GenderRules.Normalize(o.Tenant.Gender),
                BedLabel = bed.BedLabel,
                MoveIn = o.MoveInDate,
                MoveOut = o.MoveOutDate,
                IsReserved = o.MoveInDate > today,
                OverlapNote = clashes.Count == 0 ? "" : "Overlaps with " + string.Join(", ", clashes)
            };
        }).ToList();
    }

    private void Render()
    {
        RenderChips();
        RenderRooms();
        _details?.Refresh();   // keeps an open details window up to date after every action
    }

    // ---------- Room details: their own window ----------

    private RoomDetailsWindow? _details;

    internal RoomInfo? RoomFor(int id) => _rooms.FirstOrDefault(r => r.Id == id);

    // The bed chosen in the details window. The bed actions below work on it.
    internal int? BedId
    {
        get => _bedId;
        set => _bedId = value;
    }

    private void OpenDetails(int roomId)
    {
        if (_details != null) return;

        _roomId = roomId;
        _bedId = null;
        Render();   // highlights the room's card

        var window = new RoomDetailsWindow(this, roomId);
        if (Owner != null) window.Owner = Owner;
        _details = window;
        try { window.ShowDialog(); }
        finally { _details = null; }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (RoomWrap == null) return;
        RenderRooms();
    }

    private void RenderChips()
    {
        ChipPanel.Children.Clear();
        var beds = _rooms.SelectMany(r => r.Beds).ToList();

        // The counts are beds, so the first chip says so. A status nobody has right now is left out,
        // unless it is the one being filtered on.
        ChipPanel.Children.Add(MakeChip("All", $"All beds  {beds.Count}", null));
        foreach (var status in new[] { "Available", "Occupied", "Reserved", "Maintenance", "Inactive" })
        {
            var count = beds.Count(b => b.Status == status);
            if (count == 0 && _filter != status) continue;
            ChipPanel.Children.Add(MakeChip(status, $"{status}  {count}", status));
        }
    }

    private Border MakeChip(string key, string text, string? status)
    {
        var tone = status == null ? null : ToneOf(status);

        var chip = new Border
        {
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 8, 0),
            CornerRadius = new CornerRadius(14),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(1.5),
            BorderBrush = _filter == key ? Res("AppPrimaryBrush") : Brushes.Transparent,
            Background = tone == null ? Res("AppBackgroundBrush") : Res(tone + "SoftBrush"),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = tone == null ? Res("AppTextBrush") : Res(tone + "Brush")
            }
        };
        chip.MouseLeftButtonUp += (_, _) =>
        {
            _filter = key;
            Render();
        };
        return chip;
    }

    private void RenderRooms()
    {
        RoomWrap.Children.Clear();
        var query = SearchBox.Text.Trim();

        var shown = _rooms
            .Where(r => query == "" || r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                    || FloorRules.Label(r.Floor).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(r => _filter == "All" || r.Beds.Any(b => b.Status == _filter))
            .ToList();

        if (shown.Count == 0)
        {
            RoomWrap.Children.Add(new TextBlock
            {
                Text = _rooms.Count == 0
                    ? "No rooms yet. Select Add room to create the first one."
                    : "No rooms match your search or filter.",
                Style = (Style)FindResource("MutedText"),
                Margin = new Thickness(4, 8, 0, 0)
            });
            return;
        }

        // One heading per floor, with that floor's rooms wrapped underneath it.
        var floors = shown.GroupBy(r => r.Floor ?? "")
                          .OrderBy(g => g.Key == "" ? null : g.Key, Comparer<string?>.Create(FloorRules.Compare));
        foreach (var floor in floors)
        {
            var rooms = floor.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            RoomWrap.Children.Add(MakeFloorHeader(floor.Key == "" ? null : floor.Key, rooms));

            var wrap = new WrapPanel();
            foreach (var room in rooms)
                wrap.Children.Add(MakeRoomCard(room));
            RoomWrap.Children.Add(wrap);
        }
    }

    private FrameworkElement MakeFloorHeader(string? floor, List<RoomInfo> rooms)
    {
        var bedCount = rooms.Sum(r => r.Beds.Count);
        var summary = $"{rooms.Count} room{(rooms.Count == 1 ? "" : "s")}  -  {bedCount} bed{(bedCount == 1 ? "" : "s")}";
        if (floor == null) summary += "  -  edit these rooms to set their floor";

        var row = new DockPanel { Margin = new Thickness(2, 4, 0, 10), LastChildFill = true };
        row.Children.Add(new TextBlock
        {
            Text = FloorRules.Label(floor),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("AppTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });
        row.Children.Add(new TextBlock
        {
            Text = summary,
            Style = (Style)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        row.Children.Add(new Border
        {
            Height = 1,
            Background = Res("AppBorderBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    private Border MakeRoomCard(RoomInfo room)
    {
        var selected = room.Id == _roomId;

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = room.Name,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("AppTextBrush")
        });
        var count = new TextBlock
        {
            Text = room.IsActive ? Fmt.Count(room.Beds.Count, "bed") : "Inactive",
            Style = (Style)FindResource("MutedText"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(count, 1);
        header.Children.Add(count);

        var chips = new WrapPanel { Margin = new Thickness(0, 12, 0, 4) };
        if (room.Beds.Count == 0)
            chips.Children.Add(new TextBlock
            {
                Text = "No beds yet",
                Style = (Style)FindResource("MutedText"),
                Margin = new Thickness(0, 0, 0, 8)
            });
        foreach (var bed in room.Beds)
            chips.Children.Add(MakeBedChip(bed));

        var stack = new StackPanel();
        stack.Children.Add(header);
        if (room.Beds.Count > 0)
        {
            stack.Children.Add(MakeStatusBar(room));
            stack.Children.Add(MakeStatusSummary(room));
        }
        stack.Children.Add(chips);

        var card = new Border
        {
            Width = 270,
            Margin = new Thickness(0, 0, 16, 16),
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Opacity = room.IsActive ? 1 : 0.65,
            Background = Res("AppSurfaceBrush"),
            BorderBrush = selected ? Res("AppPrimaryBrush") : Res("AppBorderBrush"),
            BorderThickness = new Thickness(selected ? 2 : 1),
            Child = stack
        };
        card.MouseLeftButtonUp += (_, _) => OpenDetails(room.Id);
        card.ToolTip = "Open this room: its beds, actions and who is staying here.";
        return card;
    }

    // One colored segment per bed, in the same colors as the bed chips below it, so a room's occupancy shows at a glance.
    private static FrameworkElement MakeStatusBar(RoomInfo room)
    {
        var bar = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new Thickness(0, 10, 0, 0), Height = 6 };
        foreach (var bed in room.Beds)
            bar.Children.Add(new Border
            {
                Margin = new Thickness(0, 0, 3, 0),
                CornerRadius = new CornerRadius(3),
                Background = Res(ToneOf(bed.Status) + "Brush"),
                Opacity = bed.Status == "Inactive" ? 0.4 : 1.0,
                ToolTip = $"{bed.Label}: {bed.Status}"
            });
        return bar;
    }

    // "2 Occupied  -  1 Available": the words come first, the color only supports them.
    private static FrameworkElement MakeStatusSummary(RoomInfo room)
    {
        var text = new TextBlock { FontSize = 12, Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap };
        foreach (var status in new[] { "Occupied", "Reserved", "Available", "Maintenance", "Inactive" })
        {
            var count = room.Beds.Count(b => b.Status == status);
            if (count == 0) continue;
            if (text.Inlines.Count > 0)
                text.Inlines.Add(new System.Windows.Documents.Run("   ·   ") { Foreground = Res("AppSecondaryBrush") });
            text.Inlines.Add(new System.Windows.Documents.Run($"{count} {status}")
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = Res(ToneOf(status) + "Brush")
            });
        }
        return text;
    }

    private static Border MakeBedChip(BedInfo bed)
    {
        var tone = ToneOf(bed.Status);
        return new Border
        {
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = new CornerRadius(8),
            Background = Res(tone + "SoftBrush"),
            ToolTip = bed.HasOverlap ? ClashWarnings.BedChipTip(bed.Label, bed.Status) : $"{bed.Label}: {bed.Status}",
            Child = new TextBlock
            {
                Text = bed.HasOverlap ? "⚠ " + bed.Label : bed.Label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                MaxWidth = 170,   // long bed names ("Single Bed A - R103") need room to show in full
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Res(tone + "Brush"),
                // An inactive bed is dimmed, not struck through (a line through a name looks like a mistake).
                Opacity = bed.Status == "Inactive" ? 0.6 : 1.0
            }
        };
    }

    // ---------- Room actions ----------

    private void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        var values = FormDialog.Ask(Owner, "Add room", "Add room",
            new[] { ("Room name (e.g. Room 101)", ""), ("Floor (e.g. 1, 2 or Ground)", ""), ("Notes", "") },
            v =>
            {
                var name = v[0].Trim();
                if (name == "") return "Please enter a room name.";
                var floorError = FloorRules.Validate(v[1]);
                if (floorError != null) return floorError;
                var lower = name.ToLower();
                using var check = AppDb.Create();
                return check.Rooms.Any(r => r.RoomName.ToLower() == lower) ? "A room with that name already exists." : null;
            });
        if (values == null) return;

        using var db = AppDb.Create();
        var room = new Room { RoomName = values[0].Trim(), Floor = FloorRules.Normalize(values[1]), Notes = Blank(values[2]) };
        db.Rooms.Add(room);
        db.SaveChanges();

        Reload(room.Id);
    }

    internal void EditRoom_Click(object sender, RoutedEventArgs e)
    {
        var room = CurrentRoom;
        if (room == null) return;

        var values = FormDialog.Ask(Owner, "Edit room", "Save changes",
            new[] { ("Room name", room.Name), ("Floor (e.g. 1, 2 or Ground)", room.Floor ?? ""), ("Notes", room.Notes ?? "") },
            v =>
            {
                var name = v[0].Trim();
                if (name == "") return "Please enter a room name.";
                var floorError = FloorRules.Validate(v[1]);
                if (floorError != null) return floorError;
                var lower = name.ToLower();
                using var check = AppDb.Create();
                return check.Rooms.Any(r => r.RoomName.ToLower() == lower && r.Id != room.Id)
                                    ? "Another room already has that name." : null;
            });
        if (values == null) return;

        var changes = ConfirmWording.Changes(
            ("Room name", room.Name, values[0]),
            ("Floor", room.Floor, FloorRules.Normalize(values[1])),
            ("Notes", room.Notes, values[2]));
        if (changes.Count == 0) return;

        using var db = AppDb.Create();
        var entity = db.Rooms.Find(room.Id)!;
        entity.RoomName = values[0].Trim();
        entity.Floor = FloorRules.Normalize(values[1]);
        entity.Notes = Blank(values[2]);
        db.SaveChanges();

        Reload();
    }

    internal void ToggleRoom_Click(object sender, RoutedEventArgs e)
    {
        var room = CurrentRoom;
        if (room == null) return;

        using var db = AppDb.Create();

        if (room.IsActive && db.Occupancies.CurrentOrUpcoming().Any(o => o.Bed.RoomId == room.Id))
        {
            Say.Info("This room cannot be deactivated because it still has tenants or upcoming move-ins. Move them out first.");
            return;
        }


        var entity = db.Rooms.Find(room.Id)!;
        entity.IsActive = !room.IsActive;
        db.SaveChanges();

        Reload();
    }

    // ---------- Bed actions ----------

    internal void AddBed_Click(object sender, RoutedEventArgs e)
    {
        var room = CurrentRoom;
        if (room == null) return;
        if (!room.IsActive)
        {
            Say.Info("Beds cannot be added to an inactive room. Activate the room first.");
            return;
        }

        var values = FormDialog.Ask(Owner, $"Add bed to {room.Name}", "Add bed",
            new[] { ("Bed label (e.g. Bed A)", ""), ("Monthly rent", "") },
            v =>
            {
                var label = v[0].Trim();
                if (label == "") return "Please enter a bed label.";
                if (!TryMoney(v[1], out _)) return "Enter the monthly rent as a number (e.g. 1500).";
                using var check = AppDb.Create();
                return check.Beds.Any(b => b.RoomId == room.Id && b.BedLabel == label)
                    ? "This room already has a bed with that label." : null;
            });
        if (values == null) return;

        TryMoney(values[1], out var rate);

        using var db = AppDb.Create();
        var bed = new Bed { RoomId = room.Id, BedLabel = values[0].Trim(), MonthlyRate = Math.Round(rate, 2) };
        db.Beds.Add(bed);
        db.SaveChanges();

        _bedId = bed.Id;
        Reload();
    }

    internal void EditBed_Click(object sender, RoutedEventArgs e)
    {
        var room = CurrentRoom;
        var bed = SelectedBed;
        if (room == null || bed == null) return;

        var values = FormDialog.Ask(Owner, $"Edit bed in {room.Name}", "Save changes",
            new[] { ("Bed label", bed.Label), ("Monthly rent", bed.Rate.ToString("0.##")) },
            v =>
            {
                var label = v[0].Trim();
                if (label == "") return "Please enter a bed label.";
                if (!TryMoney(v[1], out _)) return "Enter the monthly rent as a number (e.g. 1500).";
                using var check = AppDb.Create();
                return check.Beds.Any(b => b.RoomId == room.Id && b.BedLabel == label && b.Id != bed.Id)
                    ? "Another bed in this room has that label." : null;
            },
            note: "A new rent only applies to future move-ins. Tenants already staying keep the rent they agreed to.");
        if (values == null) return;

        TryMoney(values[1], out var rate);

        var changes = ConfirmWording.Changes(
            ("Bed label", bed.Label, values[0]),
            ("Monthly rent", Fmt.Peso(bed.Rate), Fmt.Peso(Math.Round(rate, 2))));
        if (changes.Count == 0) return;
        if (!ConfirmActionDialog.Ask(Owner, "Edit bed", ConfirmWording.EditBed(room.Name, bed.Label, changes))) return;

        using var db = AppDb.Create();
        var entity = db.Beds.Find(bed.Id)!;
        entity.BedLabel = values[0].Trim();
        entity.MonthlyRate = Math.Round(rate, 2);
        db.SaveChanges();

        Reload();
    }

    internal void DeactivateBed_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = AppDb.Create();
        if (db.Occupancies.CurrentOrUpcoming().Any(o => o.BedId == bed.Id))
        {
            Say.Info("This bed cannot be deactivated because it has a tenant or an upcoming move-in. Move the tenant out first.");
            return;
        }


        var entity = db.Beds.Find(bed.Id)!;
        entity.IsActive = false;
        db.SaveChanges();
        Reload();
    }

    internal void ActivateBed_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = AppDb.Create();

        var entity = db.Beds.Find(bed.Id)!;
        entity.IsActive = true;
        db.SaveChanges();
        Reload();
    }

    internal void StartMaintenance_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = AppDb.Create();
        if (db.Occupancies.CurrentOrUpcoming().Any(o => o.BedId == bed.Id))
        {
            Say.Info("Maintenance cannot be started because this bed has a tenant or an upcoming move-in. Move the tenant out first.");
            return;
        }


        var entity = db.Beds.Find(bed.Id)!;
        entity.IsMaintenance = true;
        db.SaveChanges();
        Reload();
    }

    internal void EndMaintenance_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = AppDb.Create();

        var entity = db.Beds.Find(bed.Id)!;
        entity.IsMaintenance = false;
        db.SaveChanges();
        Reload();
    }
}