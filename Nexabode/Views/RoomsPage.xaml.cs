using System.Windows;
using System.Windows.Controls;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Views;

public partial class RoomsPage : Page
{
    public class RoomRow
    {
        public int Id { get; set; }
        public string RoomName { get; set; } = "";
        public bool IsActive { get; set; }
        public string Display => IsActive ? RoomName : $"{RoomName} (Inactive)";
    }

    public class BedRow
    {
        public int Id { get; set; }
        public string BedLabel { get; set; } = "";
        public decimal MonthlyRate { get; set; }
        public string Status { get; set; } = "";
    }

    public RoomsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadRooms();
    }

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private void LoadRooms(int? selectId = null)
    {
        using var db = new AppDbContext();
        var rooms = db.Rooms
            .OrderBy(r => r.RoomName)
            .AsEnumerable()
            .Select(r => new RoomRow { Id = r.Id, RoomName = r.RoomName, IsActive = r.IsActive })
            .ToList();

        RoomList.ItemsSource = rooms;
        if (selectId != null)
            RoomList.SelectedItem = rooms.FirstOrDefault(r => r.Id == selectId);
    }

    private void LoadBeds()
    {
        if (RoomList.SelectedItem is not RoomRow room)
        {
            BedGrid.ItemsSource = null;
            SelectedRoomText.Text = "Select a room";
            UpdateButtons();
            return;
        }

        SelectedRoomText.Text = room.Display;

        using var db = new AppDbContext();
        var occupiedBedIds = db.Occupancies
            .Where(o => o.MoveOutDate == null)
            .Select(o => o.BedId)
            .ToList();

        BedGrid.ItemsSource = db.Beds
            .Where(b => b.RoomId == room.Id)
            .OrderBy(b => b.BedLabel)
            .AsEnumerable()
            .Select(b => new BedRow
            {
                Id = b.Id,
                BedLabel = b.BedLabel,
                MonthlyRate = b.MonthlyRate,
                Status = !room.IsActive || !b.IsActive ? "Inactive"
                       : occupiedBedIds.Contains(b.Id) ? "Occupied" : "Vacant"
            })
            .ToList();

        UpdateButtons();
    }

    // Each button is enabled only when it applies to the current selection.
    private void UpdateButtons()
    {
        var room = RoomList.SelectedItem as RoomRow;
        DeactivateRoomButton.IsEnabled = room is { IsActive: true };
        ActivateRoomButton.IsEnabled = room is { IsActive: false };

        // Bed buttons only work while the room itself is active.
        var status = (BedGrid.SelectedItem as BedRow)?.Status;
        var roomActive = room is { IsActive: true };
        DeactivateButton.IsEnabled = roomActive && status is "Vacant" or "Occupied";
        ActivateButton.IsEnabled = roomActive && status == "Inactive";
    }

    private void RoomList_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadBeds();

    private void BedGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        var name = RoomNameBox.Text.Trim();
        if (name == "")
        {
            Tell("Please enter a room name.");
            return;
        }

        using var db = new AppDbContext();
        if (db.Rooms.Any(r => r.RoomName == name))
        {
            Tell("A room with that name already exists.");
            return;
        }

        var room = new Room
        {
            RoomName = name,
            Floor = string.IsNullOrWhiteSpace(FloorBox.Text) ? null : FloorBox.Text.Trim()
        };
        db.Rooms.Add(room);
        db.SaveChanges();

        RoomNameBox.Text = "";
        FloorBox.Text = "";
        LoadRooms(room.Id);
    }

    private void DeactivateRoom_Click(object sender, RoutedEventArgs e)
    {
        if (RoomList.SelectedItem is not RoomRow row)
        {
            Tell("Select a room first.");
            return;
        }

        using var db = new AppDbContext();

        if (db.Occupancies.Any(o => o.MoveOutDate == null && o.Bed.RoomId == row.Id))
        {
            Tell("This room still has tenants staying in it. Move them out first.");
            return;
        }

        var room = db.Rooms.Find(row.Id)!;
        room.IsActive = false;
        db.SaveChanges();
        LoadRooms(row.Id);
    }

    private void ActivateRoom_Click(object sender, RoutedEventArgs e)
    {
        if (RoomList.SelectedItem is not RoomRow row)
        {
            Tell("Select a room first.");
            return;
        }

        using var db = new AppDbContext();
        var room = db.Rooms.Find(row.Id)!;
        room.IsActive = true;
        db.SaveChanges();
        LoadRooms(row.Id);
    }

    private void AddBed_Click(object sender, RoutedEventArgs e)
    {
        if (RoomList.SelectedItem is not RoomRow room)
        {
            Tell("Select a room first.");
            return;
        }
        if (!room.IsActive)
        {
            Tell("This room is inactive. Activate the room before adding beds.");
            return;
        }

        var label = BedLabelBox.Text.Trim();
        if (label == "" || !decimal.TryParse(RateBox.Text, out var rate) || rate < 0)
        {
            Tell("Enter a bed label and a valid monthly rate.");
            return;
        }

        using var db = new AppDbContext();
        if (db.Beds.Any(b => b.RoomId == room.Id && b.BedLabel == label))
        {
            Tell("That bed already exists in this room.");
            return;
        }

        db.Beds.Add(new Bed { RoomId = room.Id, BedLabel = label, MonthlyRate = rate });
        db.SaveChanges();

        BedLabelBox.Text = "";
        RateBox.Text = "";
        LoadBeds();
    }

    private void Deactivate_Click(object sender, RoutedEventArgs e)
    {
        if (BedGrid.SelectedItem is not BedRow row)
        {
            Tell("Select a bed first.");
            return;
        }

        using var db = new AppDbContext();
        var bed = db.Beds.Find(row.Id)!;

        if (db.Occupancies.Any(o => o.BedId == bed.Id && o.MoveOutDate == null))
        {
            Tell("This bed is occupied. Move the tenant out first.");
            return;
        }

        bed.IsActive = false;
        db.SaveChanges();
        LoadBeds();
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (BedGrid.SelectedItem is not BedRow row)
        {
            Tell("Select a bed first.");
            return;
        }

        using var db = new AppDbContext();
        var bed = db.Beds.Find(row.Id)!;
        bed.IsActive = true;
        db.SaveChanges();
        LoadBeds();
    }
}