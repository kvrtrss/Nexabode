using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// "Forgot password?" on the sign-in screen: the Owner recovery key sets a new password for an Owner account,
// or creates a new Owner account when the username is new.
public partial class EmergencyAccessDialog : Window
{
    private string _newKey = "";
    private string _username = "";
    private bool _wasReset;
    private bool _lockedOut;

    private EmergencyAccessDialog(string usernameHint)
    {
        InitializeComponent();
        UsernameBox.Text = usernameHint;
        StrengthMeter.Attach(NewBox, () => UsernameBox.Text);
        Loaded += (_, _) => KeyBox.Focus();
    }

    // Returns true when the password was reset or the Owner account was created. newKey is the replacement key,
    // to be shown once. wasReset says which of the two happened. lockedOut is true when sign-in is now locked,
    // so the sign-in screen can show it.
    public static bool Ask(Window? owner, string usernameHint, out string username, out string newKey,
                           out bool wasReset, out bool lockedOut)
    {
        var dialog = new EmergencyAccessDialog(usernameHint);
        if (owner != null) dialog.Owner = owner;

        var done = dialog.ShowDialog() == true;
        username = done ? dialog._username : "";
        newKey = done ? dialog._newKey : "";
        wasReset = done && dialog._wasReset;
        lockedOut = dialog._lockedOut;
        return done;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        // Quick checks first, so nothing is sent that cannot work.
        if (string.IsNullOrWhiteSpace(KeyBox.Text) || string.IsNullOrWhiteSpace(UsernameBox.Text))
        {
            Fail("Enter the Owner recovery key and your username.");
            return;
        }
        if (NewBox.Password != ConfirmBox.Password)
        {
            Fail("The new passwords do not match.");
            return;
        }

        var result = AuthService.RecoverAccess(KeyBox.Text, UsernameBox.Text, NewBox.Password, ConfirmBox.Password);

        if (result.Success)
        {
            _username = result.Username ?? "";
            _newKey = result.NewEmergencyKey ?? "";
            _wasReset = result.WasReset;
            DialogResult = true;
            return;
        }

        if (result.Locked)
        {
            _lockedOut = true;
            Say.Info(result.Error ?? "Too many unsuccessful attempts.", this);
            DialogResult = false;
            return;
        }

        Fail(result.Error ?? "The password could not be reset.");
        NewBox.Password = string.Empty;
        ConfirmBox.Password = string.Empty;
    }
}
