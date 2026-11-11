using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class ForgotPasswordDialog : Window
{
    private string _newCode = "";
    private string _username = "";
    private bool _lockedOut;

    private ForgotPasswordDialog(string usernameHint)
    {
        InitializeComponent();
        UsernameBox.Text = usernameHint;
        StrengthMeter.Attach(NewBox, () => UsernameBox.Text);

        Loaded += (_, _) =>
        {
            if (usernameHint.Length == 0) UsernameBox.Focus();
            else CodeBox.Focus();
        };
    }

    // Returns true when the password was changed. newCode is the replacement recovery code, to be shown once.
    // lockedOut is true when sign-in is now locked, so the sign-in screen can show it.
    // username is the account whose password was changed, for the printed recovery sheet.
    public static bool Ask(Window? owner, string usernameHint, out string newCode, out string username, out bool lockedOut)
    {
        var dialog = new ForgotPasswordDialog(usernameHint);
        if (owner != null) dialog.Owner = owner;

        var changed = dialog.ShowDialog() == true;
        newCode = changed ? dialog._newCode : "";
        username = changed ? dialog._username : "";
        lockedOut = dialog._lockedOut;
        return changed;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Quick checks first, so nobody is asked to confirm something that cannot work.
        if (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(CodeBox.Text))
        {
            Fail("Enter your username and recovery code.");
            return;
        }
        if (NewBox.Password != ConfirmBox.Password)
        {
            Fail("The new passwords do not match.");
            return;
        }
        if (!ConfirmActionDialog.Ask(this, "Change password", ConfirmWording.RecoverPassword(UsernameBox.Text.Trim())))
            return;

        var result = AuthService.Recover(UsernameBox.Text, CodeBox.Text, NewBox.Password, ConfirmBox.Password);

        if (result.Success)
        {
            _newCode = result.NewRecoveryCode ?? "";
            _username = UsernameBox.Text.Trim();
            DialogResult = true;
            return;
        }

        // Wrong details: clear the secrets so they don't sit in the boxes.
        if (result.Locked)
        {
            _lockedOut = true;
            MessageBox.Show(this, result.Error, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Warning);
            DialogResult = false;
            return;
        }

        Fail(result.Error ?? "The password could not be changed.");
        CodeBox.Text = string.Empty;
        NewBox.Password = string.Empty;
        ConfirmBox.Password = string.Empty;
    }
}
