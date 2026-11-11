using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// An Owner chooses (or removes) the reset phrase. The phrase is never shown, saved or written to the change history:
// only a BCrypt hash of it is kept.
public partial class ResetPhraseDialog : Window
{
    private readonly string _username;

    private ResetPhraseDialog(string username, bool hasPhrase)
    {
        InitializeComponent();
        _username = username;

        if (hasPhrase)
        {
            TitleText.Text = "Change your reset phrase";
            RemoveButton.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => PhraseBox.Focus();
    }

    // Returns true when the phrase was saved or removed.
    public static bool Ask(Window? owner, string username)
    {
        var dialog = new ResetPhraseDialog(username, AuthService.HasResetPhrase());
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var error = ResetPhraseRules.Error(PhraseBox.Password, _username);
        if (error != null) { Fail(error); return; }

        if (ResetPhraseRules.Normalize(PhraseBox.Password) != ResetPhraseRules.Normalize(ConfirmBox.Password))
        {
            Fail("The two phrases do not match.");
            ConfirmBox.Password = string.Empty;
            return;
        }

        try
        {
            AuthService.SaveResetPhrase(PhraseBox.Password, "Reset phrase set in Settings");
        }
        catch (Exception ex)
        {
            Fail("The reset phrase could not be saved. Details: " + ex.Message);
            return;
        }

        DialogResult = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var sure = ConfirmActionDialog.YesNo(
            "Remove the reset phrase?\n\nIt will no longer be possible to use it to regain access. You can set a new phrase in Settings at any time.",
            "Remove reset phrase", this, confirmText: "Remove");
        if (sure != MessageBoxResult.Yes) return;

        try
        {
            AuthService.ClearResetPhrase("Reset phrase removed in Settings");
        }
        catch (Exception ex)
        {
            Fail("The reset phrase could not be removed. Details: " + ex.Message);
            return;
        }

        DialogResult = true;
    }
}
