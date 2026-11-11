using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// Asks a signed-in person for their password again before something sensitive.
public partial class ConfirmPasswordDialog : Window
{
    private readonly string _username;

    private ConfirmPasswordDialog(string username, string note)
    {
        InitializeComponent();
        _username = username;
        NoteText.Text = note;
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public static bool Ask(Window? owner, string username, string note)
    {
        var dialog = new ConfirmPasswordDialog(username, note);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.VerifyPassword(_username, PasswordInput.Password))
        {
            ErrorText.Text = "The password is not correct.";
            ErrorText.Visibility = Visibility.Visible;
            PasswordInput.Password = string.Empty;
            return;
        }

        DialogResult = true;
    }
}
