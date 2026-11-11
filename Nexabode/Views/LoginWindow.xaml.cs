using System.Windows;
using System.Windows.Threading;
using Nexabode.Services;
using Wpf.Ui.Controls;

namespace Nexabode.Views;

public partial class LoginWindow : FluentWindow
{
    private const string DefaultPassword = "admin123";

    private readonly DispatcherTimer _lockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lockEndsUtc;

    public LoginWindow()
    {
        InitializeComponent();
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);   // fits small screens; the card scrolls if needed

        _lockTimer.Tick += (_, _) => UpdateLockCountdown();
        Closed += (_, _) => _lockTimer.Stop();
        Loaded += (_, _) =>
        {
            // The lock is remembered, so reopening the app (or logging out) while locked shows it straight away.
            if (!ApplyLockState()) UsernameBox.Focus();
        };
    }

    // ---------- messages ----------

    private void HideMessages()
    {
        ErrorBanner.Visibility = Visibility.Collapsed;
        SuccessBanner.Visibility = Visibility.Collapsed;
    }

    // A short headline, and optionally a second line with more detail.
    private void ShowError(string title, string? detail = null)
    {
        HideMessages();
        ErrorTitle.Text = title;
        ErrorDetail.Text = detail ?? string.Empty;
        ErrorDetail.Visibility = detail == null ? Visibility.Collapsed : Visibility.Visible;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    private void ShowNotice(string message)
    {
        HideMessages();
        NoticeText.Text = message;
        SuccessBanner.Visibility = Visibility.Visible;
    }

    // ---------- the lock ----------

    // Shows the lock if sign-in is locked. Returns true when it is.
    private bool ApplyLockState()
    {
        if (!AuthService.IsLocked(out var remaining))
        {
            SetLocked(false);
            return false;
        }

        BeginLock(remaining);
        return true;
    }

    private void BeginLock(TimeSpan remaining)
    {
        _lockEndsUtc = DateTime.UtcNow + remaining;
        HideMessages();
        PasswordInput.Password = string.Empty;
        SetLocked(true);
        UpdateLockCountdown();
        _lockTimer.Start();
    }

    private void SetLocked(bool locked)
    {
        LockBanner.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        UsernameBox.IsEnabled = !locked;
        PasswordInput.IsEnabled = !locked;
        LoginButton.IsEnabled = !locked;
        ForgotButton.IsEnabled = !locked;
        EmergencyButton.IsEnabled = !locked;
        if (!locked) _lockTimer.Stop();
    }

    private void UpdateLockCountdown()
    {
        var left = _lockEndsUtc - DateTime.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            // Check the saved lock again rather than trusting this timer alone.
            if (!ApplyLockState()) UsernameBox.Focus();
            return;
        }

        LockText.Text = "Too many unsuccessful attempts. For your security, sign-in is locked. " +
                        $"Please try again in {LockoutRules.Countdown(left)}.";
    }

    // ---------- sign in ----------

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;
        var result = AuthService.SignIn(UsernameBox.Text, password);

        switch (result.Status)
        {
            case SignInStatus.MissingInput:
                ShowError("Enter your username and password.");
                return;

            case SignInStatus.Locked:
                BeginLock(result.LockRemaining);
                return;

            case SignInStatus.Failed:
                var left = result.AttemptsLeft;
                ShowError("Incorrect username or password.",
                          $"{left} attempt{(left == 1 ? "" : "s")} remaining before sign-in is locked.");
                PasswordInput.Password = string.Empty;
                return;
        }

        var user = result.User!;

        // Signed in with the default password: a new one must be chosen before continuing.
        var signedInName = user.Username;
        if (password == DefaultPassword || user.MustChangePassword)
        {
            if (!ChangePasswordDialog.Ask(this, user.Username, forced: true, out signedInName))
            {
                ShowError("A new password is required before you can continue.");
                PasswordInput.Password = string.Empty;
                return;
            }
        }

        Session.Administrator = signedInName;
        Session.IsOwner = user.Role == AccountRoles.Owner;
        Session.UserId = user.Id;

        // One gentle pop-up for a new account: the recovery code (and, for an Owner, the emergency key) together.
        // "Not now" is fine: the Dashboard keeps a reminder until it is done.
        SetupCodesDialog.RunFirstTime(this);

        new MainWindow().Show();   // show the main window first, then close login
        Close();
    }

    // ---------- forgot password ----------

    private void ForgotButton_Click(object sender, RoutedEventArgs e)
    {
        var changed = ForgotPasswordDialog.Ask(this, UsernameBox.Text.Trim(), out var newCode,
                                               out var accountName, out var lockedOut);

        if (changed)
        {
            RecoveryCodeDialog.ShowIssued(this, newCode, accountName);
            PasswordInput.Password = string.Empty;
            ShowNotice("Your password has been changed. Sign in with your new password.");
        }

        if (lockedOut) ApplyLockState();
    }

    // ---------- emergency access ----------

    private void EmergencyButton_Click(object sender, RoutedEventArgs e)
    {
        var created = EmergencyAccessDialog.Ask(this, out var username, out var newKey, out var usedPhrase,
                                                out var lockedOut);

        if (created)
        {
            // The emergency key is replaced when used, so the new one is shown now. The reset phrase has no
            // replacement: it is gone, and the new Owner sets a new one in Settings.
            if (newKey.Length > 0) RecoveryCodeDialog.ShowEmergencyIssued(this, newKey);
            UsernameBox.Text = username;
            PasswordInput.Password = string.Empty;
            ShowNotice($"The Owner account \"{username}\" has been created. Sign in with it to continue." +
                       (usedPhrase ? " The reset phrase has now been used and removed. Set a new one in Settings." : ""));
            PasswordInput.Focus();
        }

        if (lockedOut) ApplyLockState();
    }
}
