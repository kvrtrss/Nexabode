using System.Windows;
using Nexabode.Data;
using Nexabode.Models;
using Wpf.Ui.Controls;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class LoginWindow : FluentWindow
{
    private const string DefaultPassword = "admin123";

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordInput.Password;

        var lower = username.ToLower();

        AdminUser? user;
        using (var db = new AppDbContext())
            user = db.AdminUsers.FirstOrDefault(u => u.Username.ToLower() == lower);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            ShowError("Incorrect username or password.");
            PasswordInput.Password = string.Empty;
            return;
        }

        // Signed in with the default password: a new one must be chosen before continuing.
        var signedInName = user.Username;
        if (password == DefaultPassword)
        {
            if (!ChangePasswordDialog.Ask(this, user.Username, forced: true, out signedInName))
            {
                ShowError("You need to choose a new password before you can sign in.");
                PasswordInput.Password = string.Empty;
                return;
            }
        }

        Session.Administrator = signedInName;
        new MainWindow().Show();   // show the main window first, then close login
        Close();
    }
}