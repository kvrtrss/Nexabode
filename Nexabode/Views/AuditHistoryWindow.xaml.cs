using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexabode.Data;
using Nexabode.Services;

namespace Nexabode.Views;

// The change history. By default it reads like a timeline: one card for each action, in plain words, grouped
// by day. "Technical view" shows every stored record in a table instead, exactly as it was saved.
public partial class AuditHistoryWindow : Window
{
    // One stored record, for the technical table.
    public class Row
    {
        public string When { get; set; } = "";
        public string Administrator { get; set; } = "";
        public string Tenant { get; set; } = "";
        public string Record { get; set; } = "";
        public string Category { get; set; } = "";
        public string Field { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Exception { get; set; } = "";
    }

    private readonly List<Row> _rows = new();
    private readonly List<HistoryEvent> _events = new();
    private readonly Dictionary<int, string> _tenantNames;
    private readonly bool _forOneTenant;
    private readonly bool _ready;

    private AuditHistoryWindow(int? tenantId, string? tenantName)
    {
        InitializeComponent();
        Title = tenantName == null ? "Change history" : $"Change history: {tenantName}";
        TitleText.Text = Title;
        _forOneTenant = tenantId != null;

        CategoryBox.ItemsSource = AuditWording.Categories;
        CategoryBox.SelectedIndex = 0;

        using var db = new AppDbContext();
        var query = db.AuditLog.AsQueryable();
        if (tenantId != null) query = query.Where(a => a.TenantId == tenantId);

        var entries = query.OrderByDescending(a => a.Id).Take(1000).ToList();
        _tenantNames = db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        foreach (var a in entries)
        {
            _rows.Add(new Row
            {
                When = a.ChangedAt.ToString("yyyy-MM-dd HH:mm"),
                Administrator = a.Administrator,
                Tenant = TenantName(a.TenantId),
                Record = a.EntityType,
                Category = AuditWording.CategoryOf(a.EntityType),
                Field = a.Field,
                OldValue = a.OldValue ?? "-",
                NewValue = a.NewValue ?? "-",
                Reason = a.Reason,
                Exception = a.IsConflictException ? "Yes" : ""
            });
        }

        _events.AddRange(AuditWording.Group(entries.Select(a => new AuditItem(
            a.Id, a.ChangedAt, a.Administrator, a.EntityType, a.Field, a.OldValue, a.NewValue,
            a.Reason, a.IsConflictException, a.TenantId))));

        _ready = true;
        ApplyFilter();
    }

    // Shows the history of one tenant, or of everyone when no tenant is given.
    public static void ShowFor(Window? owner, int? tenantId = null, string? tenantName = null)
    {
        var window = new AuditHistoryWindow(tenantId, tenantName);
        if (owner != null) window.Owner = owner;
        window.ShowDialog();
    }

    private string TenantName(int? id) => id is int i && _tenantNames.TryGetValue(i, out var n) ? n : "";

    private string SelectedCategory => CategoryBox.SelectedItem as string ?? AuditWording.AllCategories;

    private Brush Res(string key) => (Brush)FindResource(key);

    // The color of a kind of change: part is "Fill", "Text" or "Bar". An unknown kind gets the neutral one.
    private Brush Cat(string category, string part) =>
        (Brush)(TryFindResource($"Cat{category}{part}Brush") ?? FindResource($"CatOther{part}Brush"));

    // ---------- Filtering ----------

    private void ApplyFilter()
    {
        if (!_ready) return;

        var query = SearchBox.Text.Trim();
        var category = SelectedCategory;
        var technical = TechCheck.IsChecked == true;

        CardScroll.Visibility = technical ? Visibility.Collapsed : Visibility.Visible;
        HistoryGrid.Visibility = technical ? Visibility.Visible : Visibility.Collapsed;

        if (technical)
        {
            var rows = _rows
                .Where(r => category == AuditWording.AllCategories || r.Category == category)
                .Where(r => query == "" || string.Join(" ", r.When, r.Administrator, r.Tenant, r.Record, r.Field,
                                                       r.OldValue, r.NewValue, r.Reason)
                                .Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            HistoryGrid.ItemsSource = rows;
            CountText.Text = _rows.Count == 0
                ? "No changes recorded yet."
                : $"{rows.Count} of {_rows.Count} records (newest first, up to 1,000)";
            return;
        }

        var events = _events
            .Where(e => category == AuditWording.AllCategories || e.Category == category)
            .Where(e => AuditWording.Matches(e, TenantName(e.TenantId), query))
            .ToList();

        DrawCards(events);
        CountText.Text = _events.Count == 0
            ? "No changes recorded yet."
            : $"{events.Count} of {_events.Count} changes (newest first)";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void Technical_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    // ---------- The timeline ----------

    private void DrawCards(List<HistoryEvent> events)
    {
        CardPanel.Children.Clear();

        if (events.Count == 0)
        {
            CardPanel.Children.Add(new TextBlock
            {
                Text = _events.Count == 0 ? "Nothing has been changed yet." : "No changes match.",
                Style = (Style)FindResource("MutedText"),
                Margin = new Thickness(0, 12, 0, 0)
            });
            return;
        }

        var today = DateTime.Now;
        string? currentDay = null;

        foreach (var e in events)
        {
            var day = AuditWording.DayLabel(e.When, today);
            if (day != currentDay)
            {
                CardPanel.Children.Add(new TextBlock
                {
                    Text = day,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Res("AppSecondaryBrush"),
                    Margin = new Thickness(0, currentDay == null ? 0 : 14, 0, 6)
                });
                currentDay = day;
            }

            CardPanel.Children.Add(MakeCard(e));
        }
    }

    private Border MakeCard(HistoryEvent e)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The kind of change, as a soft colored label: Stay indigo, Billing amber, Payment green, Account teal,
        // Security rose. The word stays on it, so the color is never the only clue.
        var badge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 2, 9, 2),
            Margin = new Thickness(0, 1, 14, 0),
            VerticalAlignment = VerticalAlignment.Top,
            MinWidth = 66,
            Background = Cat(e.Category, "Fill"),
            Child = new TextBlock
            {
                Text = e.Category,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Cat(e.Category, "Text")
            }
        };
        grid.Children.Add(badge);

        // The sentence, what else changed with it, and why.
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = e.Title,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Res("AppTextBrush")
        });

        foreach (var detail in e.Details)
            text.Children.Add(new TextBlock
            {
                Text = "\u2022  " + detail,
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Res("AppSecondaryBrush"),
                Margin = new Thickness(0, 3, 0, 0)
            });

        text.Children.Add(new TextBlock
        {
            Text = "Reason: " + e.Reason,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Res("AppSecondaryBrush"),
            Margin = new Thickness(0, 5, 0, 0)
        });

        if (e.IsException)
            text.Children.Add(new TextBlock
            {
                Text = "Saved although it overlaps another stay (accepted by the person who made the change).",
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Res("DangerBrush"),
                Margin = new Thickness(0, 5, 0, 0)
            });

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        // When, by whom, and for which tenant.
        var meta = new StackPanel { Margin = new Thickness(16, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        meta.Children.Add(new TextBlock
        {
            Text = AuditWording.TimeText(e.When),
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = Res("AppTextBrush")
        });
        meta.Children.Add(new TextBlock
        {
            Text = "by " + e.Administrator,
            FontSize = 12.5,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = Res("AppSecondaryBrush")
        });
        var tenant = TenantName(e.TenantId);
        if (!_forOneTenant && tenant != "")
            meta.Children.Add(new TextBlock
            {
                Text = tenant,
                FontSize = 12.5,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = Res("AppSecondaryBrush")
            });
        Grid.SetColumn(meta, 2);
        grid.Children.Add(meta);

        // A colored edge on the left, in the color of the kind of change, so the list can be scanned by color.
        var edge = new Border { Width = 4, Background = Cat(e.Category, "Bar"), CornerRadius = new CornerRadius(5, 0, 0, 5) };
        var content = new Border { Padding = new Thickness(14, 11, 14, 11), Child = grid };
        Grid.SetColumn(content, 1);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(edge);
        row.Children.Add(content);

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = Res("AppBorderBrush"),
            Background = Res("AppSurfaceBrush"),
            Child = row
        };
    }
}
