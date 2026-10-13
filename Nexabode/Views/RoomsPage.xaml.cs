using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Views;

public partial class RoomsPage : Page
{
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

    private void LoadRooms(int? selectId = null)
    {
        using var db = new AppDbContext();
        var rooms = db.Rooms.OrderBy(r => r.RoomName).ToList();
        RoomList.ItemsSource = rooms;
        if (selectId != null)
            RoomList.SelectedItem = rooms.FirstOrDefault(r => r.Id == selectId);
    }

    private void LoadBeds()
    {
        if (RoomList.SelectedItem is not Room room)
        {
            BedGrid.ItemsSource = null;
            SelectedRoomText.Text = "Select a room";
            return;
        }

        SelectedRoomText.Text = room.RoomName;

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
                Status = !b.IsActive ? "Inactive"
                       : occupiedBedIds.Contains(b.Id) ? "Occupied" : "Vacant"
            })
            .ToList();
    }

    private void RoomList_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadBeds();

    private void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        var name = RoomNameBox.Text.Trim();
        if (name == "")
        {
            MessageBox.Show("Please enter a room name.");
            return;
        }

        using var db = new AppDbContext();
        if (db.Rooms.Any(r => r.RoomName == name))
        {
            MessageBox.Show("A room with that name already exists.");
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

    private void AddBed_Click(object sender, RoutedEventArgs e)
    {
        if (RoomList.SelectedItem is not Room room)
        {
            MessageBox.Show("Select a room first.");
            return;
        }

        var label = BedLabelBox.Text.Trim();
        if (label == "" || !decimal.TryParse(RateBox.Text, out var rate) || rate < 0)
        {
            MessageBox.Show("Enter a bed label and a valid monthly rate.");
            return;
        }

        using var db = new AppDbContext();
        if (db.Beds.Any(b => b.RoomId == room.Id && b.BedLabel == label))
        {
            MessageBox.Show("That bed already exists in this room.");
            return;
        }

        db.Beds.Add(new Bed { RoomId = room.Id, BedLabel = label, MonthlyRate = rate });
        db.SaveChanges();

        BedLabelBox.Text = "";
        RateBox.Text = "";
        LoadBeds();
    }

    private void ToggleBed_Click(object sender, RoutedEventArgs e)
    {
        if (BedGrid.SelectedItem is not BedRow row)
        {
            MessageBox.Show("Select a bed first.");
            return;
        }

        using var db = new AppDbContext();
        var bed = db.Beds.Find(row.Id)!;

        if (bed.IsActive && db.Occupancies.Any(o => o.BedId == bed.Id && o.MoveOutDate == null))
        {
            MessageBox.Show("This bed is occupied. Move the tenant out first.");
            return;
        }

        bed.IsActive = !bed.IsActive;
        db.SaveChanges();
        LoadBeds();
    }
}