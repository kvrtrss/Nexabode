using System.Windows;

namespace Nexabode.Views;

public partial class BackupPasswordDialog : Window
{
    private readonly bool _confirm;
    private string? _result;

    private BackupPasswordDialog(string title, string okText, string note, bool confirm)
    {
        InitializeComponent();
        _confirm = confirm;

        Title = title;
        TitleText.Text = title;
        NoteText.Text = note;
        OkButton.Content = okText;
        ConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) => PasswordInput.Focus();
    }

    // Returns the password, or null if the user cancelled.
    // confirm = true asks for the password twice and enforces a minimum length (for new backups).
    public static string? Ask(Window? owner, string title, string okText, string note, bool confirm)
    {
        var dialog = new BackupPasswordDialog(title, okText, note, confirm);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._result : null;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;

        if (password.Length == 0) { Fail("Please enter the backup password."); return; }

        if (_confirm)
        {
            if (password.Length < 8) { Fail("The backup password must be at least 8 characters."); return; }
            if (password != ConfirmInput.Password) { Fail("The two passwords don't match."); return; }
        }

        _result = password;
        DialogResult = true;
    }
}