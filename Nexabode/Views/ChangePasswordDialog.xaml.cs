using System.Windows;
using Nexabode.Data;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly string _username;
    private readonly bool _forced;
    private string _finalUsername;

    private ChangePasswordDialog(string username, bool forced)
    {
        InitializeComponent();
        _username = username;
        _finalUsername = username;
        _forced = forced;

        if (forced)
        {
            Title = "Set up your account";
            TitleText.Text = "Set up your account";
            NoteText.Text = "You signed in with the default password. For security, choose your own password " +
                            "before continuing. You can also choose your own username.";
            CurrentPanel.Visibility = Visibility.Collapsed;
            UsernamePanel.Visibility = Visibility.Visible;
            UsernameBox.Text = username;
            SaveButton.Content = "Save and continue";
        }
        else
        {
            Title = "Change password";
            TitleText.Text = "Change password";
            NoteText.Text = "Enter your current password, then choose a new one.";
        }

        Loaded += (_, _) =>
        {
            if (forced) UsernameBox.Focus();
            else CurrentBox.Focus();
        };
    }

    // Returns true if the account was saved. finalUsername is the username to use from now on.
    public static bool Ask(Window? owner, string username, bool forced, out string finalUsername)
    {
        var dialog = new ChangePasswordDialog(username, forced);
        if (owner != null) dialog.Owner = owner;

        var saved = dialog.ShowDialog() == true;
        finalUsername = saved ? dialog._finalUsername : username;
        return saved;
    }

    public static bool Ask(Window? owner, string username, bool forced) =>
        Ask(owner, username, forced, out _);

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var newPassword = NewBox.Password;
        var requested = _forced ? UsernameBox.Text.Trim() : _username;

        using var db = new AppDbContext();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username == _username);
        if (user == null) { Fail("The account could not be found."); return; }

        if (_forced)
        {
            var nameError = UsernameRules.Validate(requested);
            if (requested.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                Fail("Please choose a username other than the default \"admin\".");
                return;
            }
            if (nameError != null) { Fail(nameError); return; }

            var lower = requested.ToLower();
            if (db.AdminUsers.Any(u => u.Id != user.Id && u.Username.ToLower() == lower))
            {
                Fail("That username is already taken.");
                return;
            }
        }

        if (!_forced && !BCrypt.Net.BCrypt.Verify(CurrentBox.Password, user.PasswordHash))
        {
            Fail("The current password is not correct.");
            return;
        }

        if (newPassword.Length < 8) { Fail("The new password must be at least 8 characters."); return; }
        if (newPassword == "admin123") { Fail("Please choose a password other than the default."); return; }
        if (newPassword.Equals(requested, StringComparison.OrdinalIgnoreCase))
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

        if (_forced && requested != user.Username)
        {
            AuditService.Record(db, "Account", user.Id, null, "Username", user.Username, requested,
                                "Username chosen at first sign-in");
            user.Username = requested;
        }

        db.SaveChanges();   // the password, the username and the history entry are saved together
        _finalUsername = requested;
        DialogResult = true;
    }
}