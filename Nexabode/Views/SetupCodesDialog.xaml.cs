using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// The first-time pop-up for an Owner: the recovery code and the emergency access key together, so a new
// person sees one window instead of two. Nothing is saved unless they confirm they have written it down.
public partial class SetupCodesDialog : Window
{
    private readonly string _recovery = RecoveryCodeRules.Generate();
    private readonly string _emergency = RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);
    private readonly int _userId;

    private SetupCodesDialog(int userId)
    {
        InitializeComponent();
        _userId = userId;
        RecoveryText.Text = RecoveryCodeRules.Format(_recovery);
        EmergencyText.Text = RecoveryCodeRules.Format(_emergency);
        ShowPhraseStatus();
        Loaded += (_, _) => SavedCheck.Focus();
    }

    // The optional reset phrase. It is saved by its own window as soon as it is chosen, so "Not now" below
    // does not undo it. The phrase itself is never shown here.
    private void ShowPhraseStatus()
    {
        var hasPhrase = AuthService.HasResetPhrase();
        PhraseStatus.Text = hasPhrase ? "A reset phrase is set." : "Not set yet.";
        PhraseButton.Content = hasPhrase ? "Change or remove it" : "Set a reset phrase";
    }

    private void SetPhrase_Click(object sender, RoutedEventArgs e)
    {
        // This window is only offered to a signed-in Owner who has just entered their password.
        ResetPhraseDialog.Ask(this, AuthService.UsernameOf(_userId) ?? Session.Administrator);
        ShowPhraseStatus();
    }

    // Offers whatever the signed-in account is still missing. Both missing: one combined window.
    // Only one missing: the matching single window. Returns true when something was saved.
    public static bool RunFirstTime(Window? owner)
    {
        var needRecovery = !AuthService.HasRecoveryCode(Session.UserId);
        var needEmergency = Session.IsOwner && !AuthService.HasEmergencyKey();

        if (needRecovery && needEmergency)
        {
            var dialog = new SetupCodesDialog(Session.UserId);
            if (owner != null) dialog.Owner = owner;
            return dialog.ShowDialog() == true;
        }
        if (needRecovery)
            return RecoveryCodeDialog.Issue(owner, Session.UserId, "First recovery code made at setup", firstTime: true);
        if (needEmergency)
            return RecoveryCodeDialog.IssueEmergencyKey(owner, "First emergency access key made at setup", firstTime: true);
        return false;
    }

    private void PrintRecovery_Click(object sender, RoutedEventArgs e) =>
        RecoverySheetPrinter.Print(this, RecoverySheetKind.RecoveryCode, AuthService.UsernameOf(_userId),
                                   RecoveryCodeRules.Format(_recovery));

    private void PrintEmergency_Click(object sender, RoutedEventArgs e) =>
        RecoverySheetPrinter.Print(this, RecoverySheetKind.EmergencyKey, null, RecoveryCodeRules.Format(_emergency));

    private void SavedCheck_Changed(object sender, RoutedEventArgs e) =>
        ContinueButton.IsEnabled = SavedCheck.IsChecked == true;

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AuthService.SaveRecoveryCode(_userId, _recovery, "First recovery code made at setup");
            AuthService.SaveEmergencyKey(_emergency, "First emergency access key made at setup");
        }
        catch (Exception ex)
        {
            ErrorText.Text = "The codes could not be saved. Details: " + ex.Message;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }
}
