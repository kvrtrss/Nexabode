using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// What the person chose in the Generate rent window.
public sealed record GenerateRentChoice(List<RentBillingPage.RentRow> Rows, DateOnly UsualDue, bool FullRent);

// One window for generating rent: the due date, the partial-month option and which tenants to charge, all in view.
// Clicking the button is the confirmation, so there is no separate "Are you sure?" step.
public partial class GenerateRentDialog : Window
{
    public sealed class Line
    {
        private bool _include = true;

        public Action? Changed { get; set; }
        public RentBillingPage.RentRow Row { get; init; } = null!;

        public bool Include
        {
            get => _include;
            set { _include = value; Changed?.Invoke(); }
        }

        public string Tenant => Row.Tenant;
        public string Bed => Row.Bed;
        public string Details => Row.Details;
        public string Rent => Fmt.Peso(Row.Amount);
        public string Due => Row.DueText;
    }

    private readonly Func<bool, DateOnly, List<RentBillingPage.RentRow>> _build;
    private List<Line> _lines = new();
    private DateOnly _usualDue;
    private bool _ready;
    private GenerateRentChoice? _choice;

    private GenerateRentDialog(string monthName, DateOnly usualDue, bool fullRent,
                               Func<bool, DateOnly, List<RentBillingPage.RentRow>> build)
    {
        InitializeComponent();
        _build = build;
        _usualDue = usualDue;

        TitleText.Text = $"Generate rent for {monthName}";
        DuePicker.SelectedDate = usualDue.ToDateTime(TimeOnly.MinValue);
        FullRentCheck.IsChecked = fullRent;

        _ready = true;
        Rebuild();
    }

    // 'build' works out the rent that is still to be generated for a given partial-month option and due date.
    // Returns null when the window is cancelled.
    public static GenerateRentChoice? Ask(Window? owner, string monthName, DateOnly usualDue, bool fullRent,
                                          Func<bool, DateOnly, List<RentBillingPage.RentRow>> build)
    {
        var dialog = new GenerateRentDialog(monthName, usualDue, fullRent, build);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._choice : null;
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (DuePicker.SelectedDate is DateTime picked) _usualDue = DateOnly.FromDateTime(picked);
        Rebuild();
    }

    // Recalculates the list, keeping the tenants the person already switched off.
    private void Rebuild()
    {
        var skipped = _lines.Where(l => !l.Include).Select(l => l.Row.OccupancyId).ToHashSet();

        _lines = _build(FullRentCheck.IsChecked == true, _usualDue)
            .Select(row => new Line { Row = row, Include = !skipped.Contains(row.OccupancyId), Changed = UpdateSummary })
            .ToList();

        LineGrid.ItemsSource = _lines;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var chosen = _lines.Where(l => l.Include).ToList();

        GenerateButton.IsEnabled = chosen.Count > 0;
        GenerateButton.Content = chosen.Count == 1 ? "Generate 1 charge" : $"Generate {chosen.Count} charges";
        SummaryText.Text = _lines.Count == 0
            ? "There is no rent left to generate for this month."
            : $"{chosen.Count} of {_lines.Count} selected, totalling {Fmt.Peso(chosen.Sum(l => l.Row.Amount))}. " +
              "A charge can be voided later in Billing Charges if it was created by mistake.";
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        _choice = new GenerateRentChoice(
            _lines.Where(l => l.Include).Select(l => l.Row).ToList(),
            _usualDue,
            FullRentCheck.IsChecked == true);
        DialogResult = true;
    }
}
