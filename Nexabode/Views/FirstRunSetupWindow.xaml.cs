using System.Windows;
using Nexabode.Services;
using Wpf.Ui.Controls;

namespace Nexabode.Views;

// Shown on a brand-new install, before the sign-in screen: the person creates the first Owner account,
// then is given the Owner recovery key. There is no default account to find and guess.
public partial class FirstRunSetupWindow : FluentWindow
{
    public FirstRunSetupWindow()
    {
        InitializeComponent();
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);   // fits small screens; the card scrolls if needed
        StrengthMeter.Attach(NewBox, () => UsernameBox.Text);
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var username = UsernameBox.Text.Trim();
        var password = NewBox.Password;

        var nameError = UsernameRules.Validate(username);
        if (nameError == null && username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            nameError = "Choose a username other than \"admin\".";
        if (nameError != null) { Fail(nameError); return; }

        var passwordError = AccountService.PasswordError(username, password);
        if (passwordError != null) { Fail(passwordError); return; }

        if (password != ConfirmBox.Password) { Fail("The two passwords do not match."); return; }

        // The recovery key is shown next. Nothing is saved until the person confirms they have written it down.
        var saved = RecoveryCodeDialog.IssueOwnerKey(this, key => AuthService.CreateFirstOwner(username, password, key));
        if (!saved) return;   // they chose Back to change something

        var login = new LoginWindow();
        login.Prefill(username, "Your Owner account is ready. Sign in to continue.");
        login.Show();   // show the sign-in window first, then close this one
        Close();
    }
}
