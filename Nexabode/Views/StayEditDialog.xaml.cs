using System.Globalization;
using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class StayEditDialog : Window
{
    private readonly StayEdit _original;
    private readonly Func<StayEdit, string?> _hardCheck;
    private readonly Func<StayEdit, string?> _conflictText;
    private StayEdit? _result;
    private string? _acknowledged;   // the exact warning the exception box was ticked for

    private StayEditDialog(string title, string? note, List<BedChoice> beds, StayEdit current,
                           Func<StayEdit, string?> hardCheck, Func<StayEdit, string?> conflictText)
    {
        InitializeComponent();
        _original = current;
        _hardCheck = hardCheck;
        _conflictText = conflictText;

        Title = title;
        TitleText.Text = title;

        if (!string.IsNullOrWhiteSpace(note))
        {
            NoteText.Text = note;
            NoteText.Visibility = Visibility.Visible;
        }

        BedCombo.ItemsSource = beds;
        BedCombo.SelectedItem = beds.FirstOrDefault(b => b.Id == current.BedId);
        MoveInPicker.SelectedDate = current.MoveIn.ToDateTime(TimeOnly.MinValue);

        if (current.MoveOut is DateOnly moveOut)
            MoveOutPicker.SelectedDate = moveOut.ToDateTime(TimeOnly.MinValue);
        OngoingCheck.IsChecked = current.MoveOut == null;
        MoveOutPicker.IsEnabled = current.MoveOut != null;

        RentBox.Text = current.Rent.ToString("0.##", CultureInfo.CurrentCulture);
        DepositBox.Text = current.Deposit == 0 ? "" : current.Deposit.ToString("0.##", CultureInfo.CurrentCulture);
    }

    // Returns the confirmed edit, or null if the administrator cancelled.
    // hardCheck: an error message that always blocks saving (or null).
    // conflictText: a description of overlapping stays (or null). Overlaps can be saved only as an explicit exception.
    public static StayEdit? Ask(Window? owner, string title, string? note, List<BedChoice> beds,
                                StayEdit current, Func<StayEdit, string?> hardCheck,
                                Func<StayEdit, string?> conflictText)
    {
        var dialog = new StayEditDialog(title, note, beds, current, hardCheck, conflictText);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._result : null;
    }

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value >= 0;

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Ongoing_Click(object sender, RoutedEventArgs e) =>
        MoveOutPicker.IsEnabled = OngoingCheck.IsChecked != true;

    private bool SameAsOriginal(StayEdit r) =>
        r.BedId == _original.BedId && r.MoveIn == _original.MoveIn && r.MoveOut == _original.MoveOut &&
        r.Rent == _original.Rent && r.Deposit == _original.Deposit;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (BedCombo.SelectedItem is not BedChoice bed) { Fail("Choose a bed."); return; }
        if (MoveInPicker.SelectedDate is not DateTime inDate) { Fail("Choose the move-in date."); return; }

        DateOnly? moveOut = null;
        if (OngoingCheck.IsChecked != true)
        {
            if (MoveOutPicker.SelectedDate is not DateTime outDate)
            {
                Fail("Choose the move-out date, or tick \"Ongoing stay\".");
                return;
            }
            moveOut = DateOnly.FromDateTime(outDate);
        }

        if (!TryMoney(RentBox.Text, out var rent)) { Fail("Enter the monthly rent as a number (e.g. 1500)."); return; }

        decimal deposit = 0;
        if (DepositBox.Text.Trim() != "" && !TryMoney(DepositBox.Text, out deposit))
        {
            Fail("Enter the deposit as a number, or leave it blank.");
            return;
        }

        var result = new StayEdit
        {
            BedId = bed.Id,
            MoveIn = DateOnly.FromDateTime(inDate),
            MoveOut = moveOut,
            Rent = Math.Round(rent, 2),
            Deposit = Math.Round(deposit, 2),
            Reason = ReasonBox.Text.Trim()
        };

        if (SameAsOriginal(result)) { Fail("Nothing was changed. Press Cancel to close."); return; }

        // Always blocked: a move-out before the move-in.
        if (!StayRules.IsValidRange(result.MoveIn, result.MoveOut))
        {
            Fail("The move-out date can't be before the move-in date.");
            return;
        }

        var hardError = _hardCheck(result);
        if (hardError != null) { Fail(hardError); return; }

        if (result.Reason.Length == 0)
        {
            Fail("Enter a reason for this change. It is saved in the audit trail.");
            return;
        }

        // Overlaps: warn, then require an explicit exception for exactly this warning.
        var conflicts = _conflictText(result);
        if (!string.IsNullOrEmpty(conflicts))
        {
            ConflictText.Text = conflicts;
            ConflictPanel.Visibility = Visibility.Visible;

            if (ExceptionCheck.IsChecked != true || _acknowledged != conflicts)
            {
                ExceptionCheck.IsChecked = false;
                _acknowledged = conflicts;
                Fail("These stays would overlap. Read the warning, tick the exception box if this is " +
                     "really correct, then press Save changes again.");
                return;
            }
            result.ConflictException = true;
        }
        else
        {
            ConflictPanel.Visibility = Visibility.Collapsed;
            ExceptionCheck.IsChecked = false;
            _acknowledged = null;
        }

        _result = result;
        DialogResult = true;
    }
}