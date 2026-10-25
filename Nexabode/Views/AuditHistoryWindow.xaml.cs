using System.Windows;
using System.Windows.Controls;
using Nexabode.Data;

namespace Nexabode.Views;

public partial class AuditHistoryWindow : Window
{
    public class Row
    {
        public string When { get; set; } = "";
        public string Administrator { get; set; } = "";
        public string Tenant { get; set; } = "";
        public string Record { get; set; } = "";
        public string Field { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Exception { get; set; } = "";
    }

    private readonly List<Row> _all = new();

    private AuditHistoryWindow(int? tenantId, string? tenantName)
    {
        InitializeComponent();
        Title = tenantName == null ? "Change history" : $"Change history - {tenantName}";
        TitleText.Text = Title;

        using var db = new AppDbContext();
        var query = db.AuditLog.AsQueryable();
        if (tenantId != null) query = query.Where(a => a.TenantId == tenantId);

        var entries = query.OrderByDescending(a => a.Id).Take(1000).ToList();
        var names = db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        foreach (var a in entries)
        {
            _all.Add(new Row
            {
                When = a.ChangedAt.ToString("yyyy-MM-dd HH:mm"),
                Administrator = a.Administrator,
                Tenant = a.TenantId is int id && names.TryGetValue(id, out var name) ? name : "",
                Record = a.EntityType,
                Field = a.Field,
                OldValue = a.OldValue ?? "-",
                NewValue = a.NewValue ?? "-",
                Reason = a.Reason,
                Exception = a.IsConflictException ? "Yes" : ""
            });
        }

        ApplyFilter();
    }

    // Shows the history of one tenant, or of everyone when no tenant is given.
    public static void ShowFor(Window? owner, int? tenantId = null, string? tenantName = null)
    {
        var window = new AuditHistoryWindow(tenantId, tenantName);
        if (owner != null) window.Owner = owner;
        window.ShowDialog();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        var rows = query == ""
            ? _all
            : _all.Where(r => string.Join(" ", r.When, r.Administrator, r.Tenant, r.Record, r.Field,
                                          r.OldValue, r.NewValue, r.Reason)
                         .Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        HistoryGrid.ItemsSource = rows;
        CountText.Text = _all.Count == 0
            ? "No changes recorded yet."
            : $"{rows.Count} of {_all.Count} entries (newest first, up to 1,000)";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
}