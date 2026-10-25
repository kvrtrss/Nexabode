using System.Windows;
using Nexabode.Data;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class ChangeUsernameDialog : Window
{
    private readonly string _username;
    private string _newUsername;

    private ChangeUsernameDialog(string username)
    {
        InitializeComponent();
        _username = username;
        _newUsername = username;

        NoteText.Text = $"You are signed in as {username}. Use the new username the next time you sign in.";
        UsernameBox.Text = username;
        Loaded += (_, _) =>
        {
            UsernameBox.Focus();
            UsernameBox.SelectAll();
        };
    }

    // Returns true if the username was changed. newUsername is the name to use from now on.
    public static bool Ask(Window? owner, string username, out string newUsername)
    {
        var dialog = new ChangeUsernameDialog(username);
        if (owner != null) dialog.Owner = owner;

        var saved = dialog.ShowDialog() == true;
        newUsername = saved ? dialog._newUsername : username;
        return saved;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var requested = UsernameBox.Text.Trim();

        var nameError = UsernameRules.Validate(requested);
        if (nameError != null) { Fail(nameError); return; }
        if (requested == _username) { Fail("That is already your username."); return; }

        using var db = new AppDbContext();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username == _username);
        if (user == null) { Fail("The account could not be found."); return; }

        if (!BCrypt.Net.BCrypt.Verify(PasswordInput.Password, user.PasswordHash))
        {
            Fail("The password is not correct.");
            return;
        }

        if (requested.Equals(PasswordInput.Password, StringComparison.OrdinalIgnoreCase))
        {
            Fail("The username can't be the same as your password.");
            return;
        }

        var lower = requested.ToLower();
        if (db.AdminUsers.Any(u => u.Id != user.Id && u.Username.ToLower() == lower))
        {
            Fail("That username is already taken.");
            return;
        }

        // The history entry is written under the old name, because that is who made the change.
        AuditService.Record(db, "Account", user.Id, null, "Username", user.Username, requested,
                            "Username changed in Settings");
        user.Username = requested;
        db.SaveChanges();

        Session.Administrator = requested;
        _newUsername = requested;
        DialogResult = true;
    }
}