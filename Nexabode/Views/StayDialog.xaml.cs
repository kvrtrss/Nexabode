using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Nexabode.Services;

namespace Nexabode.Views;

public class BedChoice
{
    public int Id { get; set; }
    public string Display { get; set; } = "";
    public decimal Rate { get; set; }
}

public class MoveInResult
{
    public int BedId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Rent { get; set; }
    public decimal Deposit { get; set; }
}

public partial class StayDialog : Window
{
    private readonly bool _isMoveIn;
    private Func<MoveInResult, string?>? _validateIn;
    private MoveInResult? _inResult;
    private DateOnly? _outResult;
    private DateOnly _min, _max;

    private StayDialog(bool isMoveIn, string title, string okText, string? note)
    {
        InitializeComponent();
        _isMoveIn = isMoveIn;
        Title = title;
        TitleText.Text = title;
        OkButton.Content = okText;

        MoveInPanel.Visibility = isMoveIn ? Visibility.Visible : Visibility.Collapsed;
        MoveOutPanel.Visibility = isMoveIn ? Visibility.Collapsed : Visibility.Visible;

        if (!string.IsNullOrWhiteSpace(note))
        {
            NoteText.Text = note;
            NoteText.Visibility = Visibility.Visible;
        }

        // Picking a bed fills in that bed's usual rent (the user can still change it).
        BedCombo.SelectionChanged += (_, _) =>
        {
            if (BedCombo.SelectedItem is BedChoice bed)
                RentBox.Text = bed.Rate.ToString("0.##", CultureInfo.CurrentCulture);
        };
    }

    public static MoveInResult? AskMoveIn(Window? owner, string tenantName, List<BedChoice> beds,
                                          Func<MoveInResult, string?>? validate)
    {
        var dialog = new StayDialog(true, $"Move in - {tenantName}", "Move in", null)
        {
            _validateIn = validate
        };
        dialog.BedCombo.ItemsSource = beds;
        dialog.BedCombo.SelectedIndex = beds.Count > 0 ? 0 : -1;
        dialog.MoveInDate.SelectedDate = DateTime.Today;
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._inResult : null;
    }

    public static DateOnly? AskMoveOut(Window? owner, string tenantName, DateOnly min, DateOnly max, string? note)
    {
        var dialog = new StayDialog(false, $"Move out - {tenantName}", "Move out", note)
        {
            _min = min,
            _max = max
        };
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = today < min ? min : today > max ? max : today;
        dialog.MoveOutDate.SelectedDate = start.ToDateTime(TimeOnly.MinValue);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._outResult : null;
    }

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value >= 0;

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_isMoveIn)
        {
            if (BedCombo.SelectedItem is not BedChoice bed) { Fail("Choose a bed."); return; }
            if (MoveInDate.SelectedDate is not DateTime date) { Fail("Choose the move-in date."); return; }
            if (!TryMoney(RentBox.Text, out var rent)) { Fail("Enter the monthly rent as a number (e.g. 1500)."); return; }

            decimal deposit = 0;
            if (DepositBox.Text.Trim() != "" && !TryMoney(DepositBox.Text, out deposit))
            {
                Fail("Enter the deposit as a number, or leave it blank.");
                return;
            }

            var result = new MoveInResult
            {
                BedId = bed.Id,
                Date = DateOnly.FromDateTime(date),
                Rent = Math.Round(rent, 2),
                Deposit = Math.Round(deposit, 2)
            };

            var error = _validateIn?.Invoke(result);
            if (error != null) { Fail(error); return; }

            _inResult = result;
        }
        else
        {
            if (MoveOutDate.SelectedDate is not DateTime date) { Fail("Choose the move-out date."); return; }

            var chosen = DateOnly.FromDateTime(date);
            if (chosen < _min) { Fail($"The move-out date can't be before the move-in date ({Fmt.Date(_min)})."); return; }
            if (chosen > _max) { Fail($"The move-out date can't be after {Fmt.Date(_max)}."); return; }

            _outResult = chosen;
        }

        DialogResult = true;
    }
}