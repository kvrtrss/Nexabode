using System.Windows;
using System.Windows.Controls;
using Nexabode.Data;

namespace Nexabode.Views;

public partial class SettingsPage : Page
{
    private string _username = "admin";

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        using var db = new AppDbContext();
        _username = db.AdminUsers.OrderBy(u => u.Id).Select(u => u.Username).FirstOrDefault() ?? "admin";

        AccountText.Text = $"Signed in as {_username}. Use a password only you know.";
        DataPathText.Text = DatabaseKeyProvider.AppFolder;
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (ChangePasswordDialog.Ask(Window.GetWindow(this), _username, forced: false))
            MessageBox.Show("Your password has been changed.", "Nexabode",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }
}