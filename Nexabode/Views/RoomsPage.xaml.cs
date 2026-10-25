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
        public string BedLabel { get; set; } = "";
        public DateOnly MoveIn { get; set; }
        public bool IsReserved { get; set; }
        public DateOnly? MoveOut { get; set; }
        public string OverlapNote { get; set; } = "";
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

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private Window? Owner => Window.GetWindow(this);

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value >= 0;

    private static string ToneOf(string status) => status switch
    {
        "Available" => "Success",
        "Occupied" => "Occupied",
        "Reserved" => "Warning",
        "Maintenance" => "Danger",
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
    private BedInfo? SelectedBed => BedGrid.SelectedItem as BedInfo;

    private void Reload(int? selectRoom = null)
    {
        if (selectRoom != null && selectRoom != _roomId)
        {
            _roomId = selectRoom;
            _bedId = null;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();

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
                Floor = r.Floor,
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
                Name = o.Tenant.FullName,
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
        RenderDetail();
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

        ChipPanel.Children.Add(MakeChip("All", $"All rooms  {_rooms.Count}", null));
        foreach (var status in new[] { "Available", "Occupied", "Reserved", "Maintenance", "Inactive" })
            ChipPanel.Children.Add(MakeChip(status, $"{status}  {beds.Count(b => b.Status == status)}", status));
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
                FontSize = 12.5,
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
            .Where(r => query == "" || r.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(r => _filter == "All" || r.Beds.Any(b => b.Status == _filter))
            .ToList();

        if (shown.Count == 0)
        {
            RoomWrap.Children.Add(new TextBlock
            {
                Text = _rooms.Count == 0
                    ? "No rooms yet. Click Add room to create your first room."
                    : "No rooms match your search or filter.",
                Style = (Style)FindResource("MutedText"),
                Margin = new Thickness(4, 8, 0, 0)
            });
            return;
        }

        foreach (var room in shown)
            RoomWrap.Children.Add(MakeRoomCard(room));
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
            Text = room.IsActive ? $"{room.Beds.Count} bed(s)" : "Inactive",
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

        var link = new TextBlock
        {
            Text = "View room details  →",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("AppPrimaryBrush"),
            Margin = new Thickness(0, 6, 0, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(chips);
        stack.Children.Add(link);

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
        card.MouseLeftButtonUp += (_, _) =>
        {
            _roomId = room.Id;
            _bedId = null;
            Render();
        };
        return card;
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
            ToolTip = bed.HasOverlap ? $"{bed.Label}: {bed.Status} - overlapping stays, check the occupants" : $"{bed.Label}: {bed.Status}",
            Child = new TextBlock
            {
                Text = bed.HasOverlap ? "⚠ " + bed.Label : bed.Label,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                MaxWidth = 110,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Res(tone + "Brush"),
                TextDecorations = bed.Status == "Inactive" ? System.Windows.TextDecorations.Strikethrough : null
            }
        };
    }

    private void RenderDetail()
    {
        var room = CurrentRoom;
        DetailEmpty.Visibility = room == null ? Visibility.Visible : Visibility.Collapsed;
        DetailBody.Visibility = room == null ? Visibility.Collapsed : Visibility.Visible;
        if (room == null) return;

        DetailTitle.Text = room.IsActive ? room.Name : $"{room.Name} (Inactive)";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(room.Floor)) parts.Add(room.Floor!);
        parts.Add($"{room.Beds.Count} bed(s)");
        if (!string.IsNullOrWhiteSpace(room.Notes)) parts.Add(room.Notes!);
        DetailSub.Text = string.Join("  -  ", parts);

        RoomToggleButton.Content = room.IsActive ? "Deactivate room" : "Activate room";

        var keep = _bedId;   // setting the list clears the selection, so remember it first
        BedGrid.ItemsSource = room.Beds;
        BedGrid.SelectedItem = room.Beds.FirstOrDefault(b => b.Id == keep);
        _bedId = keep;

        UpdateBedButtons();
        RenderOccupants();
    }

    // Shows who is staying in the selected bed, or in the whole room when no bed is selected.
    private void RenderOccupants()
    {
        if (OccupantPanel == null) return;   // the page is still being built

        var room = CurrentRoom;
        if (room == null) return;

        var bed = room.Beds.FirstOrDefault(b => b.Id == _bedId);

        List<OccupantInfo> shown;
        if (bed != null)
        {
            OccupantTitle.Text = $"Staying in {bed.Label}";
            shown = bed.Occupants;
        }
        else
        {
            OccupantTitle.Text = $"Staying in {room.Name}";
            shown = room.Beds.SelectMany(b => b.Occupants).ToList();
        }

        OccupantPanel.Children.Clear();

        if (shown.Count == 0)
        {
            OccupantPanel.Children.Add(new TextBlock
            {
                Text = bed != null
                    ? "No one is staying in this bed right now."
                    : "No one is staying in this room right now.",
                Style = (Style)FindResource("MutedText"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var occupant in shown)
        {
            // With a bed selected the bed is already known, so only the dates are shown.
            var detail = bed != null ? "" : occupant.BedLabel + "  -  ";
            detail += occupant.IsReserved
                ? $"Reserved, moves in {Fmt.Date(occupant.MoveIn)}"
                : $"Since {Fmt.Date(occupant.MoveIn)}";
            if (occupant.MoveOut is DateOnly leaving) detail += $", until {Fmt.Date(leaving)}";

            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            stack.Children.Add(new TextBlock
            {
                Text = occupant.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("AppTextBrush")
            });
            stack.Children.Add(new TextBlock
            {
                Text = detail,
                Style = (Style)FindResource("MutedText"),
                FontSize = 12.5
            });
            if (occupant.OverlapNote != "")
                stack.Children.Add(new TextBlock
                {
                    Text = occupant.OverlapNote,
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Res("DangerBrush")
                });
            OccupantPanel.Children.Add(stack);
        }
    }

    private void BedGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _bedId = SelectedBed?.Id;
        UpdateBedButtons();
        RenderOccupants();
    }

    // Each button is enabled only when it applies to the current selection.
    private void UpdateBedButtons()
    {
        var room = CurrentRoom;
        var bed = SelectedBed;
        var roomOn = room?.IsActive == true;

        AddBedButton.IsEnabled = roomOn;
        EditBedButton.IsEnabled = bed != null;
        DeactivateBedButton.IsEnabled = bed != null && roomOn && bed.IsActive;
        ActivateBedButton.IsEnabled = bed != null && roomOn && !bed.IsActive;
        StartMaintButton.IsEnabled = bed?.Status == "Available";
        EndMaintButton.IsEnabled = bed?.Status == "Maintenance";
    }

    // ---------- Room actions ----------

    private void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        var values = FormDialog.Ask(Owner, "Add room", "Add room",
            new[] { ("Room name (e.g. Room 101)", ""), ("Floor (optional)", ""), ("Notes (optional)", "") },
            v =>
            {
                var name = v[0].Trim();
                if (name == "") return "Please enter a room name.";
                var lower = name.ToLower();
                using var check = new AppDbContext();
                return check.Rooms.Any(r => r.RoomName.ToLower() == lower) ? "A room with that name already exists." : null;
            });
        if (values == null) return;

        using var db = new AppDbContext();
        var room = new Room { RoomName = values[0].Trim(), Floor = Blank(values[1]), Notes = Blank(values[2]) };
        db.Rooms.Add(room);
        db.SaveChanges();

        Reload(room.Id);
    }

    private void EditRoom_Click(object sender, RoutedEventArgs e)
    {
        var room = CurrentRoom;
        if (room == null) return;

        var values = FormDialog.Ask(Owner, "Edit room", "Save changes",
            new[] { ("Room name", room.Name), ("Floor (optional)", room.Floor ?? ""), ("Notes (optional)", room.Notes ?? "") },
            v =>
            {
                var name = v[0].Trim();
                if (name == "") return "Please enter a room name.";
                var lower = name.ToLower();
                using var check = new AppDbContext();
                return check.Rooms.Any(r => r.RoomName.ToLower() == lower && r.Id != room.Id)
                                    ? "Another room already has that name." : null;
            });
        if (values == null) return;

        using var db = new AppDbContext();
        var entity = db.Rooms.Find(room.Id)!;
        entity.RoomName = values[0].Trim();
        entity.Floor = Blank(values[1]);
        entity.Notes = Blank(values[2]);
        db.SaveChanges();

        Reload();
    }

    private void ToggleRoom_Click(object sender, RoutedEventArgs e)
    {
        var room = CurrentRoom;
        if (room == null) return;

        using var db = new AppDbContext();

        if (room.IsActive && db.Occupancies.CurrentOrUpcoming().Any(o => o.Bed.RoomId == room.Id))
        {
            Tell("This room still has tenants (or upcoming move-ins). Move them out first.");
            return;
        }

        var entity = db.Rooms.Find(room.Id)!;
        entity.IsActive = !room.IsActive;
        db.SaveChanges();

        Reload();
    }

    // ---------- Bed actions ----------

    private void AddBed_Click(object sender, RoutedEventArgs e)
    {
        var room = CurrentRoom;
        if (room == null) return;
        if (!room.IsActive)
        {
            Tell("This room is inactive. Activate the room before adding beds.");
            return;
        }

        var values = FormDialog.Ask(Owner, $"Add bed to {room.Name}", "Add bed",
            new[] { ("Bed label (e.g. Bed A)", ""), ("Monthly rent", "") },
            v =>
            {
                var label = v[0].Trim();
                if (label == "") return "Please enter a bed label.";
                if (!TryMoney(v[1], out _)) return "Enter the monthly rent as a number (e.g. 1500).";
                using var check = new AppDbContext();
                return check.Beds.Any(b => b.RoomId == room.Id && b.BedLabel == label)
                    ? "This room already has a bed with that label." : null;
            });
        if (values == null) return;

        TryMoney(values[1], out var rate);

        using var db = new AppDbContext();
        var bed = new Bed { RoomId = room.Id, BedLabel = values[0].Trim(), MonthlyRate = Math.Round(rate, 2) };
        db.Beds.Add(bed);
        db.SaveChanges();

        _bedId = bed.Id;
        Reload();
    }

    private void EditBed_Click(object sender, RoutedEventArgs e)
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
                using var check = new AppDbContext();
                return check.Beds.Any(b => b.RoomId == room.Id && b.BedLabel == label && b.Id != bed.Id)
                    ? "Another bed in this room has that label." : null;
            },
            note: "A new rent only applies to future move-ins. Tenants already staying keep the rent they agreed to.");
        if (values == null) return;

        TryMoney(values[1], out var rate);

        using var db = new AppDbContext();
        var entity = db.Beds.Find(bed.Id)!;
        entity.BedLabel = values[0].Trim();
        entity.MonthlyRate = Math.Round(rate, 2);
        db.SaveChanges();

        Reload();
    }

    private void DeactivateBed_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = new AppDbContext();
        if (db.Occupancies.CurrentOrUpcoming().Any(o => o.BedId == bed.Id))
        {
            Tell("This bed has a tenant (or an upcoming move-in). Move the tenant out first.");
            return;
        }

        var entity = db.Beds.Find(bed.Id)!;
        entity.IsActive = false;
        db.SaveChanges();
        Reload();
    }

    private void ActivateBed_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = new AppDbContext();
        var entity = db.Beds.Find(bed.Id)!;
        entity.IsActive = true;
        db.SaveChanges();
        Reload();
    }

    private void StartMaintenance_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = new AppDbContext();
        if (db.Occupancies.CurrentOrUpcoming().Any(o => o.BedId == bed.Id))
        {
            Tell("This bed has a tenant. Move the tenant out before starting maintenance.");
            return;
        }

        var entity = db.Beds.Find(bed.Id)!;
        entity.IsMaintenance = true;
        db.SaveChanges();
        Reload();
    }

    private void EndMaintenance_Click(object sender, RoutedEventArgs e)
    {
        var bed = SelectedBed;
        if (bed == null) return;

        using var db = new AppDbContext();
        var entity = db.Beds.Find(bed.Id)!;
        entity.IsMaintenance = false;
        db.SaveChanges();
        Reload();
    }
}