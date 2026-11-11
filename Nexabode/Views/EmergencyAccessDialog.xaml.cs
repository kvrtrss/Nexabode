using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class EmergencyAccessDialog : Window
{
    private string _newKey = "";
    private string _username = "";
    private bool _lockedOut;
    private bool _ready;

    private EmergencyAccessDialog()
    {
        InitializeComponent();
        _ready = true;
        StrengthMeter.Attach(NewBox, () => UsernameBox.Text);
        Loaded += (_, _) => KeyBox.Focus();
    }

    // Returns true when the Owner account was created. newKey is the replacement emergency key, to be shown once
    // (empty when the reset phrase was used instead: it has no replacement). usedPhrase says which way was used.
    // lockedOut is true when sign-in is now locked, so the sign-in screen can show it.
    public static bool Ask(Window? owner, out string username, out string newKey, out bool usedPhrase, out bool lockedOut)
    {
        var dialog = new EmergencyAccessDialog();
        if (owner != null) dialog.Owner = owner;

        var created = dialog.ShowDialog() == true;
        username = created ? dialog._username : "";
        newKey = created ? dialog._newKey : "";
        usedPhrase = created && dialog.PhraseMode.IsChecked == true;
        lockedOut = dialog._lockedOut;
        return created;
    }

    private bool UsingPhrase => PhraseMode.IsChecked == true;

    // Switches the box between the emergency key and the reset phrase.
    private void Way_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;   // the radio buttons are set up while the window is still being built

        var phrase = UsingPhrase;
        KeyBox.Visibility = phrase ? Visibility.Collapsed : Visibility.Visible;
        PhraseBox.Visibility = phrase ? Visibility.Visible : Visibility.Collapsed;
        SecretLabel.Text = phrase ? "Reset phrase" : "Emergency access key";
        IntroText.Text = phrase
            ? "Enter the reset phrase that an Owner set in Settings, then choose a username and password for a new Owner account. The phrase can be used only once."
            : "Enter the emergency access key that was saved when Nexabode was set up, then choose a username and password for a new Owner account.";
        ErrorText.Visibility = Visibility.Collapsed;
        (phrase ? (UIElement)PhraseBox : KeyBox).Focus();
    }
    private void Create_Click(object sender, RoutedEventArgs e)
    {
        // Quick checks first, so nobody is asked to confirm something that cannot work.
        var secret = UsingPhrase ? PhraseBox.Password : KeyBox.Text;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(UsernameBox.Text))
        {
            ErrorText.Text = $"Enter the {(UsingPhrase ? "reset phrase" : "emergency access key")} " +
                             "and a username for the new Owner account.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        if (NewBox.Password != ConfirmBox.Password)
        {
            ErrorText.Text = "The new passwords do not match.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        if (!ConfirmActionDialog.Ask(this, "Create Owner account", ConfirmWording.CreateOwner(UsernameBox.Text.Trim())))
            return;

        var result = UsingPhrase
            ? AuthService.ResetPhraseAccess(PhraseBox.Password, UsernameBox.Text, NewBox.Password, ConfirmBox.Password)
            : AuthService.EmergencyAccess(KeyBox.Text, UsernameBox.Text, NewBox.Password, ConfirmBox.Password);

        if (result.Success)
        {
            _username = result.Username ?? "";
            _newKey = result.NewEmergencyKey ?? "";
            DialogResult = true;
            return;
        }

        if (result.Locked)
        {
            _lockedOut = true;
            MessageBox.Show(this, result.Error, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Warning);
            DialogResult = false;
            return;
        }

        ErrorText.Text = result.Error ?? "The account could not be created.";
        ErrorText.Visibility = Visibility.Visible;
        NewBox.Password = string.Empty;
        ConfirmBox.Password = string.Empty;
    }
}
