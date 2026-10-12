using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;

namespace Nexabode.Views;

public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadStats();
    }

    private void LoadStats()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        DateText.Text = DateTime.Today.ToString("dddd, MMMM d, yyyy");

        using var db = new AppDbContext();

        var totalBeds = db.Beds.Count(b => b.IsActive);
        var occupied = db.Occupancies.Count(o => o.MoveOutDate == null);

        var collected = db.Payments
            .Where(p => p.PaymentDate >= monthStart)
            .AsEnumerable()
            .Sum(p => p.Amount);

        var overdue = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.DueDate < today)
            .AsEnumerable()
            .Sum(c => Math.Max(0, c.Amount - c.Allocations.Sum(a => a.AmountApplied)));

        OccupiedText.Text = occupied.ToString();
        VacantText.Text = Math.Max(0, totalBeds - occupied).ToString();
        CollectedText.Text = collected.ToString("C");
        OverdueText.Text = overdue.ToString("C");
    }
}