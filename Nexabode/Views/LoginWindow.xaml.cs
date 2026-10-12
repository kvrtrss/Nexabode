using System.Windows;
using Nexabode.Data;
using Wpf.Ui.Controls;

namespace Nexabode.Views;

public partial class LoginWindow : FluentWindow
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordInput.Password;

        using var db = new AppDbContext();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username == username);

        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            new MainWindow().Show();   // show the main window first, then close login
            Close();
        }
        else
        {
            ErrorText.Text = "Incorrect username or password.";
            ErrorText.Visibility = Visibility.Visible;
            PasswordInput.Password = string.Empty;
        }
    }
}