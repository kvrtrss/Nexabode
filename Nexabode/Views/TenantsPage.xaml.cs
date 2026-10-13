using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Views;

public partial class TenantsPage : Page
{
    public class TenantRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string? Contact { get; set; }
        public string? SchoolOrWork { get; set; }
        public string CurrentBed { get; set; } = "";
        public string MoveIn { get; set; } = "";
        public string Status { get; set; } = "";
        public int? OccupancyId { get; set; }
    }

    public class BedOption
    {
        public int Id { get; set; }
        public string Display { get; set; } = "";
        public decimal Rate { get; set; }
    }

    public TenantsPage()
    {
        InitializeComponent();
        MoveInPicker.SelectedDate = DateTime.Today;
        MoveOutPicker.SelectedDate = DateTime.Today;
        Loaded += (_, _) => { LoadTenants(); LoadVacantBeds(); };
    }

    private void LoadTenants(int? selectId = null)
    {
        using var db = new AppDbContext();

        var active = db.Occupancies
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .Where(o => o.MoveOutDate == null)
            .ToList();

        var rows = db.Tenants.OrderBy(t => t.FullName).ToList().Select(t =>
        {
            var occ = active.FirstOrDefault(o => o.TenantId == t.Id);
            return new TenantRow
            {
                Id = t.Id,
                FullName = t.FullName,
                Contact = t.Contact,
                SchoolOrWork = t.SchoolOrWork,
                CurrentBed = occ == null ? "-" : $"{occ.Bed.Room.RoomName} / {occ.Bed.BedLabel}",
                MoveIn = occ == null ? "-" : occ.MoveInDate.ToString("MMM d, yyyy"),
                Status = occ == null ? "Not staying" : "Staying",
                OccupancyId = occ?.Id
            };
        }).ToList();

        TenantGrid.ItemsSource = rows;
        if (selectId != null)
            TenantGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == selectId);
    }

    private void LoadVacantBeds()
    {
        using var db = new AppDbContext();

        var occupiedIds = db.Occupancies
            .Where(o => o.MoveOutDate == null)
            .Select(o => o.BedId)
            .ToList();

        BedCombo.ItemsSource = db.Beds
            .Include(b => b.Room)
            .Where(b => b.IsActive)
            .ToList()
            .Where(b => !occupiedIds.Contains(b.Id))
            .OrderBy(b => b.Room.RoomName).ThenBy(b => b.BedLabel)
            .Select(b => new BedOption
            {
                Id = b.Id,
                Display = $"{b.Room.RoomName} - {b.BedLabel}  ({b.MonthlyRate:N2})",
                Rate = b.MonthlyRate
            })
            .ToList();
    }

    private void BedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BedCombo.SelectedItem is BedOption bed)
            RentBox.Text = bed.Rate.ToString("0.##");
    }

    private void TenantGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void AddTenant_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name == "")
        {
            MessageBox.Show("Please enter the tenant's full name.");
            return;
        }

        using var db = new AppDbContext();
        var tenant = new Tenant
        {
            FullName = name,
            Contact = NullIfBlank(ContactBox.Text),
            EmergencyContact = NullIfBlank(EmergencyBox.Text),
            SchoolOrWork = NullIfBlank(SchoolBox.Text)
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        NameBox.Text = ContactBox.Text = EmergencyBox.Text = SchoolBox.Text = "";
        LoadTenants(tenant.Id);
    }

    private void MoveIn_Click(object sender, RoutedEventArgs e)
    {
        if (TenantGrid.SelectedItem is not TenantRow row)
        {
            MessageBox.Show("Select a tenant first.");
            return;
        }
        if (row.OccupancyId != null)
        {
            MessageBox.Show("This tenant is already staying in a bed. Move them out first.");
            return;
        }
        if (BedCombo.SelectedItem is not BedOption bed)
        {
            MessageBox.Show("Select a vacant bed.");
            return;
        }
        if (MoveInPicker.SelectedDate is not DateTime moveIn)
        {
            MessageBox.Show("Select a move-in date.");
            return;
        }
        if (!decimal.TryParse(RentBox.Text, out var rent) || rent < 0)
        {
            MessageBox.Show("Enter a valid monthly rent.");
            return;
        }

        decimal deposit = 0;
        if (DepositBox.Text.Trim() != "" && (!decimal.TryParse(DepositBox.Text, out deposit) || deposit < 0))
        {
            MessageBox.Show("Enter a valid deposit amount.");
            return;
        }

        using var db = new AppDbContext();

        if (db.Occupancies.Any(o => o.BedId == bed.Id && o.MoveOutDate == null))
        {
            MessageBox.Show("That bed was just taken. Please choose another.");
            LoadVacantBeds();
            return;
        }

        db.Occupancies.Add(new Occupancy
        {
            TenantId = row.Id,
            BedId = bed.Id,
            MoveInDate = DateOnly.FromDateTime(moveIn),
            AgreedRent = rent,
            Deposit = deposit
        });
        db.SaveChanges();

        RentBox.Text = DepositBox.Text = "";
        LoadTenants(row.Id);
        LoadVacantBeds();
    }

    private void MoveOut_Click(object sender, RoutedEventArgs e)
    {
        if (TenantGrid.SelectedItem is not TenantRow row || row.OccupancyId == null)
        {
            MessageBox.Show("Select a tenant who is currently staying.");
            return;
        }
        if (MoveOutPicker.SelectedDate is not DateTime moveOut)
        {
            MessageBox.Show("Select a move-out date.");
            return;
        }

        using var db = new AppDbContext();
        var occ = db.Occupancies.Find(row.OccupancyId)!;
        var date = DateOnly.FromDateTime(moveOut);

        if (date < occ.MoveInDate)
        {
            MessageBox.Show("Move-out date cannot be before the move-in date.");
            return;
        }

        occ.MoveOutDate = date;
        db.SaveChanges();

        LoadTenants(row.Id);
        LoadVacantBeds();
    }

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}