using System.Windows;
using Nexabode.Data;

namespace Nexabode.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly string _username;
    private readonly bool _forced;

    private ChangePasswordDialog(string username, bool forced)
    {
        InitializeComponent();
        _username = username;
        _forced = forced;

        if (forced)
        {
            Title = "Choose a new password";
            TitleText.Text = "Choose a new password";
            NoteText.Text = "You signed in with the default password. For security, choose your own before continuing.";
            CurrentPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            Title = "Change password";
            TitleText.Text = "Change password";
            NoteText.Text = "Enter your current password, then choose a new one.";
        }

        Loaded += (_, _) => (forced ? NewBox : CurrentBox).Focus();
    }

    // Returns true if the password was changed.
    public static bool Ask(Window? owner, string username, bool forced)
    {
        var dialog = new ChangePasswordDialog(username, forced);
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
        var newPassword = NewBox.Password;

        using var db = new AppDbContext();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username == _username);
        if (user == null) { Fail("The account could not be found."); return; }

        if (!_forced && !BCrypt.Net.BCrypt.Verify(CurrentBox.Password, user.PasswordHash))
        {
            Fail("The current password is not correct.");
            return;
        }

        if (newPassword.Length < 8) { Fail("The new password must be at least 8 characters."); return; }
        if (newPassword == "admin123") { Fail("Please choose a password other than the default."); return; }
        if (newPassword.Equals(_username, StringComparison.OrdinalIgnoreCase))
        {
            Fail("The password can't be the same as the username.");
            return;
        }
        if (newPassword != ConfirmBox.Password) { Fail("The two new passwords don't match."); return; }
        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
        {
            Fail("The new password must be different from the current one.");
            return;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        db.SaveChanges();

        DialogResult = true;
    }
}