using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;

namespace Nexabode.Views;

public partial class ReportsPage : Page
{
    public class TenantOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private Func<FlowDocument>? _current;   // rebuilds the current report (used for preview and printing)
    private string _title = "Nexabode report";

    public ReportsPage()
    {
        InitializeComponent();
        FromPicker.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToPicker.SelectedDate = DateTime.Today;
        ReportCombo.SelectedIndex = 0;
        Loaded += (_, _) => LoadTenants();
    }

    private static void Tell(string message) =>
        MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

    private void LoadTenants()
    {
        using var db = new AppDbContext();
        TenantCombo.ItemsSource = db.Tenants.OrderBy(t => t.FullName).ToList()
            .Select(t => new TenantOption { Id = t.Id, Name = t.FullName })
            .ToList();
    }

    private void ReportCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TenantPanel == null || DatePanel == null) return;
        TenantPanel.Visibility = ReportCombo.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        DatePanel.Visibility = ReportCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        switch (ReportCombo.SelectedIndex)
        {
            case 0:
                if (TenantCombo.SelectedItem is not TenantOption tenant)
                {
                    Tell("Select a tenant first.");
                    return;
                }
                _title = $"Billing statement - {tenant.Name}";
                _current = () => BuildStatement(tenant.Id);
                break;

            case 1:
                _title = "Occupancy report";
                _current = BuildOccupancy;
                break;

            case 2:
                if (FromPicker.SelectedDate is not DateTime from || ToPicker.SelectedDate is not DateTime to)
                {
                    Tell("Select both the From and To dates.");
                    return;
                }
                if (to < from)
                {
                    Tell("The To date can't be before the From date.");
                    return;
                }
                var fromD = DateOnly.FromDateTime(from);
                var toD = DateOnly.FromDateTime(to);
                _title = "Collection report";
                _current = () => BuildCollection(fromD, toD);
                break;

            default:
                Tell("Choose a report first.");
                return;
        }

        Viewer.Document = _current();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
        {
            Tell("Generate a report first, then print it.");
            return;
        }

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var doc = _current();   // fresh copy sized for the paper
        doc.PagePadding = new Thickness(50);
        doc.PageHeight = dialog.PrintableAreaHeight;
        doc.PageWidth = dialog.PrintableAreaWidth;
        doc.ColumnWidth = dialog.PrintableAreaWidth;

        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, _title);
    }

    // ---------- Document helpers ----------

    private static FlowDocument NewDoc() => new()
    {
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12,
        Background = Brushes.White,
        Foreground = Brushes.Black,
        PagePadding = new Thickness(30),
        ColumnWidth = 10000   // one wide column in the preview
    };

    private static Paragraph P(string text, double size = 12, bool bold = false, double bottom = 4) => new(new Run(text))
    {
        FontSize = size,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Margin = new Thickness(0, 0, 0, bottom)
    };

    private static TableCell Cell(string text, bool right, bool bold = false) => new(
        new Paragraph(new Run(text))
        {
            Margin = new Thickness(0),
            TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
        })
    {
        Padding = new Thickness(4, 3, 4, 3),
        BorderBrush = Brushes.Silver,
        BorderThickness = new Thickness(0, 0, 0, 0.5)
    };

    private static Table MakeTable(string[] headers, double[] widths, IEnumerable<string[]> rows,
                                   params int[] rightColumns)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 14) };
        foreach (var w in widths)
            table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });

        var group = new TableRowGroup();

        var head = new TableRow { Background = Brushes.LightGray };
        for (var i = 0; i < headers.Length; i++)
            head.Cells.Add(Cell(headers[i], rightColumns.Contains(i), bold: true));
        group.Rows.Add(head);

        foreach (var row in rows)
        {
            var tr = new TableRow();
            for (var i = 0; i < row.Length; i++)
                tr.Cells.Add(Cell(row[i], rightColumns.Contains(i)));
            group.Rows.Add(tr);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static void AddHeader(FlowDocument doc, string title, string? subtitle = null)
    {
        doc.Blocks.Add(P("Nexabode - Boarding House", 11, bottom: 0));
        doc.Blocks.Add(P(title, 22, bold: true, bottom: 2));
        if (subtitle != null) doc.Blocks.Add(P(subtitle, 11, bottom: 10));
    }

    private static string Money(decimal value) => value.ToString("N2");
    private static string D(DateOnly date) => date.ToString("MMM d, yyyy");

    // ---------- 1. Billing statement ----------

    private FlowDocument BuildStatement(int tenantId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();
        var tenant = db.Tenants.Find(tenantId)!;

        var stay = db.Occupancies
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .Where(o => o.TenantId == tenantId)
            .ToList()
            .OrderByDescending(o => o.MoveInDate)
            .FirstOrDefault();

        var charges = db.Charges
            .Include(c => c.Allocations)
            .Where(c => c.Occupancy.TenantId == tenantId)
            .ToList()
            .OrderBy(c => c.BillingMonth).ThenBy(c => c.DueDate).ThenBy(c => c.Id)
            .ToList();

        var payments = db.Payments
            .Where(p => p.TenantId == tenantId && !p.IsVoided)
            .ToList()
            .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id)
            .ToList();

        var doc = NewDoc();
        AddHeader(doc, "Billing Statement", $"Statement date: {D(today)}");

        doc.Blocks.Add(P($"Tenant: {tenant.FullName}", 13, bold: true, bottom: 0));
        if (!string.IsNullOrWhiteSpace(tenant.Contact))
            doc.Blocks.Add(P($"Contact: {tenant.Contact}", bottom: 0));
        if (stay != null)
        {
            doc.Blocks.Add(P($"Bed: {stay.Bed.Room.RoomName} / {stay.Bed.BedLabel}", bottom: 0));
            doc.Blocks.Add(P($"Move-in: {D(stay.MoveInDate)}" +
                             (stay.MoveOutDate is DateOnly mo ? $"     Move-out: {D(mo)}" : "     Status: Staying"),
                             bottom: 10));
        }

        doc.Blocks.Add(P("Charges", 14, bold: true, bottom: 0));
        if (charges.Count == 0)
        {
            doc.Blocks.Add(P("No charges have been billed yet."));
        }
        else
        {
            doc.Blocks.Add(MakeTable(
                new[] { "Month", "Description", "Due", "Amount", "Paid", "Balance" },
                new[] { 1.0, 2.2, 1.3, 1.1, 1.1, 1.1 },
                charges.Select(c =>
                {
                    var paid = c.Allocations.Sum(a => a.AmountApplied);
                    return new[]
                    {
                        c.BillingMonth,
                        c.Description ?? c.ChargeType.ToString(),
                        D(c.DueDate),
                        Money(c.Amount),
                        Money(paid),
                        Money(c.Amount - paid)
                    };
                }),
                3, 4, 5));
        }

        var billed = charges.Sum(c => c.Amount);
        var paidTotal = charges.Sum(c => c.Allocations.Sum(a => a.AmountApplied));
        var overdue = charges
            .Where(c => c.DueDate < today)
            .Sum(c => Math.Max(0, c.Amount - c.Allocations.Sum(a => a.AmountApplied)));

        doc.Blocks.Add(P("Payments received", 14, bold: true, bottom: 0));
        if (payments.Count == 0)
        {
            doc.Blocks.Add(P("No payments recorded yet."));
        }
        else
        {
            doc.Blocks.Add(MakeTable(
                new[] { "Date", "Method", "Reference", "Amount" },
                new[] { 1.4, 1.4, 2.0, 1.2 },
                payments.Select(p => new[]
                {
                    D(p.PaymentDate), p.Method ?? "", p.ReferenceNo ?? "", Money(p.Amount)
                }),
                3));
        }

        doc.Blocks.Add(P("Summary", 14, bold: true, bottom: 2));
        doc.Blocks.Add(P($"Total billed:   {Money(billed)}", bottom: 0));
        doc.Blocks.Add(P($"Total paid:   {Money(paidTotal)}", bottom: 0));
        doc.Blocks.Add(P($"Overdue now:   {Money(overdue)}", bottom: 0));
        doc.Blocks.Add(P($"BALANCE DUE:   {Money(billed - paidTotal)}", 15, bold: true, bottom: 10));

        return doc;
    }

    // ---------- 2. Occupancy report ----------

    private FlowDocument BuildOccupancy()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();
        var beds = db.Beds.Include(b => b.Room).ToList()
            .OrderBy(b => b.Room.RoomName).ThenBy(b => b.BedLabel)
            .ToList();

        var active = db.Occupancies
            .Include(o => o.Tenant)
            .Where(o => o.MoveOutDate == null)
            .ToList()
            .GroupBy(o => o.BedId)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = beds.Select(b =>
        {
            active.TryGetValue(b.Id, out var occ);
            var status = !b.IsActive || !b.Room.IsActive || b.IsMaintenance ? "Inactive"
                       : occ != null ? "Occupied" : "Vacant";
            return new
            {
                Status = status,
                Cells = new[]
                {
                    b.Room.RoomName,
                    b.BedLabel,
                    status,
                    occ?.Tenant.FullName ?? "-",
                    occ == null ? "-" : D(occ.MoveInDate),
                    Money(occ?.AgreedRent ?? b.MonthlyRate)
                }
            };
        }).ToList();

        var usable = rows.Count(r => r.Status != "Inactive");
        var occupied = rows.Count(r => r.Status == "Occupied");
        var rate = usable == 0 ? 0 : occupied * 100.0 / usable;

        var doc = NewDoc();
        AddHeader(doc, "Occupancy Report", $"As of {D(today)}");

        doc.Blocks.Add(P($"Beds in use: {usable}     Occupied: {occupied}     " +
                         $"Vacant: {usable - occupied}     Occupancy rate: {rate:0.#}%", 12, bold: true, bottom: 8));

        if (rows.Count == 0)
            doc.Blocks.Add(P("No beds have been added yet."));
        else
            doc.Blocks.Add(MakeTable(
                new[] { "Room", "Bed", "Status", "Tenant", "Move-in", "Rent" },
                new[] { 1.3, 1.3, 1.1, 2.2, 1.4, 1.1 },
                rows.Select(r => r.Cells),
                5));

        doc.Blocks.Add(P("Rent shows the tenant's agreed rent for occupied beds, and the bed's rate otherwise.", 10));
        return doc;
    }

    // ---------- 3. Collection report ----------

    private FlowDocument BuildCollection(DateOnly from, DateOnly to)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        using var db = new AppDbContext();

        var payments = db.Payments
            .Include(p => p.Tenant)
            .ToList()
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to && !p.IsVoided)
            .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id)
            .ToList();

        var charges = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .ToList();

        var overdueByTenant = charges
            .Where(c => c.DueDate < today)
            .GroupBy(c => c.Occupancy.Tenant.FullName)
            .Select(g => new
            {
                Name = g.Key,
                Amount = g.Sum(c => Math.Max(0, c.Amount - c.Allocations.Sum(a => a.AmountApplied)))
            })
            .Where(x => x.Amount > 0)
            .OrderByDescending(x => x.Amount)
            .ToList();

        var doc = NewDoc();
        AddHeader(doc, "Collection Report", $"Period: {D(from)} to {D(to)}");

        doc.Blocks.Add(P("Payments received", 14, bold: true, bottom: 0));
        if (payments.Count == 0)
        {
            doc.Blocks.Add(P("No payments were received in this period."));
        }
        else
        {
            doc.Blocks.Add(MakeTable(
                new[] { "Date", "Tenant", "Method", "Reference", "Amount" },
                new[] { 1.3, 2.3, 1.2, 1.6, 1.2 },
                payments.Select(p => new[]
                {
                    D(p.PaymentDate), p.Tenant.FullName, p.Method ?? "", p.ReferenceNo ?? "", Money(p.Amount)
                }),
                4));

            doc.Blocks.Add(P("Totals by method", 14, bold: true, bottom: 0));
            doc.Blocks.Add(MakeTable(
                new[] { "Method", "Payments", "Total" },
                new[] { 2.0, 1.0, 1.4 },
                payments.GroupBy(p => string.IsNullOrWhiteSpace(p.Method) ? "Not specified" : p.Method)
                        .OrderByDescending(g => g.Sum(p => p.Amount))
                        .Select(g => new[] { g.Key, g.Count().ToString(), Money(g.Sum(p => p.Amount)) }),
                1, 2));

            doc.Blocks.Add(P($"TOTAL COLLECTED:   {Money(payments.Sum(p => p.Amount))}", 15, bold: true, bottom: 12));
        }

        doc.Blocks.Add(P($"Overdue balances as of {D(today)}", 14, bold: true, bottom: 0));
        if (overdueByTenant.Count == 0)
        {
            doc.Blocks.Add(P("No overdue balances."));
        }
        else
        {
            doc.Blocks.Add(MakeTable(
                new[] { "Tenant", "Overdue amount" },
                new[] { 3.0, 1.4 },
                overdueByTenant.Select(x => new[] { x.Name, Money(x.Amount) }),
                1));
            doc.Blocks.Add(P($"TOTAL OVERDUE:   {Money(overdueByTenant.Sum(x => x.Amount))}", 13, bold: true));
        }

        return doc;
    }
}