using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Nexabode.Data;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class SettingsPage : Page
{
    private string _username = "admin";

    public class AccountRow
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
    }

    private AccountRow? SelectedAccount => AccountGrid.SelectedItem as AccountRow;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private Window? Owner => Window.GetWindow(this);

    private void Load()
    {
        using var db = AppDb.Create();
        _username = Session.Administrator;   // the account that is signed in now, not simply the first one

        AccountText.Text = $"Signed in as {_username} ({(Session.IsOwner ? "Owner" : "Staff")}). Keep your password private.";

        // Restoring replaces every account and all data, so it is for Owners only.
        RestoreButton.IsEnabled = Session.IsOwner;
        RestoreButton.ToolTip = Session.IsOwner ? null : "Only an Owner can restore a backup.";
        LoadAccounts();
        DataPathText.Text = DatabaseKeyProvider.AppFolder;
        VersionText.Text = $"Version {typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)}";
        ShowLastBackup();
        LoadAppearance();
        LoadBilling();
        var rulesOn = DbInitializer.CountActiveSafetyRules(db);
        RulesText.Text = rulesOn == DbInitializer.SafetyRuleCount
            ? $"All {DbInitializer.SafetyRuleCount} data safety checks are on."
            : $"Only {rulesOn} of {DbInitializer.SafetyRuleCount} data safety checks are on, so please make a backup soon.";
    }

    private bool _loadingAppearance;

    private void LoadAppearance()
    {
        _loadingAppearance = true;
        foreach (var item in AppearanceCombo.Items.OfType<ComboBoxItem>())
            if ((string?)item.Tag == ThemeService.Choice) AppearanceCombo.SelectedItem = item;
        _loadingAppearance = false;
    }

    private void Appearance_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingAppearance || AppearanceCombo.SelectedItem is not ComboBoxItem { Tag: string choice }) return;
        if (choice == ThemeService.Choice) return;

        // Only rebuild the window when the colors really change. "Follow Windows" while Windows already matches just saves the choice.
        if (ThemeService.WouldBeDark(choice) == ThemeService.IsDark) ThemeService.SetChoice(choice);
        else (Window.GetWindow(this) as MainWindow)?.SwitchTheme(choice);
    }

    private bool _loadingDueDay;

    private void LoadBilling()
    {
        _loadingDueDay = true;
        if (RentDueDayCombo.Items.Count == 0)
            for (var day = 1; day <= 28; day++) RentDueDayCombo.Items.Add(day);
        RentDueDayCombo.SelectedItem = Math.Clamp(AppSettings.Load().RentDueDay, 1, 28);
        _loadingDueDay = false;
    }

    private void RentDueDay_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingDueDay || RentDueDayCombo.SelectedItem is not int day) return;
        AppSettings.Update(s => s.RentDueDay = day);   // saved at once; the Rent Billing page reads it each time
    }

    private void ShowLastBackup()
    {
        var settings = AppSettings.Load();
        var last = BackupReminder.LastBackupTime();

        if (last == null && string.IsNullOrWhiteSpace(settings.LastBackup))
        {
            LastBackupText.Text = "No backup has been created yet. Create one before you start entering real data.";
            LastBackupText.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
        }
        else if (last == null)
        {
            LastBackupText.Text = $"Last backup: {settings.LastBackup}. Create a new backup to turn on the backup reminder.";
            LastBackupText.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
        }
        else
        {
            var days = BackupReminder.DaysSinceBackup();
            var age = days == 0 ? "today" : $"{Fmt.Count(days ?? 0, "day")} ago";
            LastBackupText.Text = $"Last backup: {last.Value.ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)} ({age})";
            LastBackupText.Foreground = (System.Windows.Media.Brush)FindResource(
                BackupReminder.IsDue ? "WarningBrush" : "SuccessBrush");
        }
    }

    // ---------- Security ----------

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (ChangePasswordDialog.Ask(Owner, _username, forced: false))
            Say.Info("Your password has been changed.");
    }

    // Makes a fresh Owner recovery key (Owners only: this whole card is hidden from Staff).
    private void EmergencyKey_Click(object sender, RoutedEventArgs e)
    {
        if (!Session.IsOwner) return;
        if (!ConfirmPasswordDialog.Ask(Owner, _username,
                "Enter your password to create a new recovery key. The current key will stop working."))
            return;

        RecoveryCodeDialog.IssueKey(Owner, "New recovery key created in Settings");
        ShowRecoveryStatus();
    }

    private void ChangeUsername_Click(object sender, RoutedEventArgs e)
    {
        if (!ChangeUsernameDialog.Ask(Owner, _username, out var newName)) return;

        _username = newName;
        AccountText.Text = $"Signed in as {_username} ({(Session.IsOwner ? "Owner" : "Staff")}). Keep your password private.";
        LoadAccounts();
        (Window.GetWindow(this) as MainWindow)?.RefreshAccount();

        Say.Info($"Your username is now {newName}. Use it the next time you sign in.");
    }

    // ---------- Accounts (Owners only) ----------


    private void LoadAccounts()
    {
        AccountsCard.Visibility = Session.IsOwner ? Visibility.Visible : Visibility.Collapsed;
        if (!Session.IsOwner) return;

        var keep = SelectedAccount?.Id;

        using var db = AppDb.Create();
        var rows = db.AdminUsers.ToList()
            .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .Select(u => new AccountRow
            {
                Id = u.Id,
                Username = u.Username,
                Role = u.Role,
                Status = u.Username.Equals(Session.Administrator, StringComparison.OrdinalIgnoreCase) ? "You (signed in)"
                       : u.MustChangePassword ? "Must choose a password at next sign-in"
                       : ""
            })
            .ToList();

        AccountGrid.ItemsSource = rows;
        AccountGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == keep);
        UpdateAccountButtons();

        OwnerWarning.Visibility = AccountService.OwnerCount(db) < 2 ? Visibility.Visible : Visibility.Collapsed;
        ShowRecoveryStatus();
    }

    // Says whether the recovery key is ready, so nobody finds out on the day it is needed.
    private void ShowRecoveryStatus()
    {
        RecoveryStatusText.Text = AuthService.HasEmergencyKey()
            ? "Owner recovery key: created."
            : "Owner recovery key: not created yet. Select New recovery key to make one.";
    }

    private void UpdateAccountButtons()
    {
        var row = SelectedAccount;
        var isSelf = row != null && row.Username.Equals(Session.Administrator, StringComparison.OrdinalIgnoreCase);

        ResetPasswordButton.IsEnabled = row != null && !isSelf;   // your own password: use Change password above
        ChangeRoleButton.IsEnabled = row != null && !isSelf;
        RemoveAccountButton.IsEnabled = row != null && !isSelf;
    }

    private void AccountGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAccountButtons();

    private void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var v = FormDialog.Ask(Owner, "Add account", "Continue",
            new[]
            {
                ("Username", ""),
                ("Role", AccountRoles.Staff)
            },
            vals =>
            {
                using var db = AppDb.Create();
                var nameError = AccountService.NameError(db, vals[0]);
                if (nameError != null) return nameError;
                return AccountRoles.Normalize(vals[1]) == null ? "Choose a role." : null;
            },
            "Staff can work with tenants, rooms, billing, payments and reports. " +
            "An Owner can also manage accounts and restore backups.",
            choices: new string[]?[] { null, new[] { AccountRoles.Staff, AccountRoles.Owner } });
        if (v == null) return;

        var username = v[0].Trim();
        var role = AccountRoles.Normalize(v[1])!;

        var password = BackupPasswordDialog.Ask(Owner, $"Temporary password for {username}", "Create account",
            "Choose a temporary password and give it to the new user. They will be asked to choose their own " +
            "password and username the first time they sign in.",
            confirm: true, noun: "temporary password");
        if (password == null) return;

        string? error;
        using (var db = AppDb.Create())
            error = AccountService.Add(db, username, role, password);
        if (error != null)
        {
            Say.Info(error);
            return;
        }

        LoadAccounts();
        Say.Info($"The {role} account \"{username}\" was added.\n\n" +
             "They can sign in with the temporary password and will then be asked to choose their own password.");
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedAccount;
        if (row == null) return;

        var password = BackupPasswordDialog.Ask(Owner, $"Reset password for {row.Username}", "Reset password",
            "Choose a temporary password and give it to them. They will be asked to choose their own password the next time they sign in.",
            confirm: true, noun: "temporary password");
        if (password == null) return;

        string? error;
        using (var db = AppDb.Create())
            error = AccountService.ResetPassword(db, row.Id, password);
        if (error != null)
        {
            Say.Info(error);
            return;
        }

        LoadAccounts();
        Say.Info($"The password for \"{row.Username}\" was reset. They will be asked to choose a new one the next time they sign in.");
    }

    private void ChangeRole_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedAccount;
        if (row == null) return;

        // A choice box first, like Add account, so the person sees and picks the role they are changing to.
        // It starts on the other role, since that is what "Change role" almost always means.
        var otherRole = row.Role == AccountRoles.Owner ? AccountRoles.Staff : AccountRoles.Owner;
        var picked = FormDialog.Ask(Owner, $"Change role: {row.Username}", "Continue",
            new[] { ("Role", otherRole) },
            vals => ConfirmWording.RoleChoiceError(row.Username, row.Role, AccountRoles.Normalize(vals[0])),
            ConfirmWording.RoleChoiceNote(row.Username, row.Role),
            choices: new string[]?[] { new[] { AccountRoles.Staff, AccountRoles.Owner } });
        if (picked == null) return;
        var newRole = AccountRoles.Normalize(picked[0])!;

        // The message that was always shown stays: it says what the new role can and cannot do.
        var confirm = ConfirmActionDialog.YesNo(
            $"Change \"{row.Username}\" from {row.Role} to {newRole}?\n\n" +
            (newRole == AccountRoles.Owner
                ? "They will be able to manage accounts and restore backups. You can change this back at any time."
                : "They will no longer be able to manage accounts or restore backups. You can change this back at any time."),
            "Change role", confirmText: "Change role");
        if (confirm != MessageBoxResult.Yes) return;

        string? error;
        using (var db = AppDb.Create())
            error = AccountService.SetRole(db, row.Id, newRole);
        if (error != null)
        {
            Say.Info(error);
            return;
        }

        LoadAccounts();
    }

    private void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedAccount;
        if (row == null) return;

        var confirm = ConfirmActionDialog.YesNo(
            $"Remove the account \"{row.Username}\"?\n\n" +
            "They will no longer be able to sign in. Changes they made stay in the change history under their name. This cannot be undone.",
            "Remove account", confirmText: "Remove account");
        if (confirm != MessageBoxResult.Yes) return;

        string? error;
        using (var db = AppDb.Create())
            error = AccountService.Remove(db, row.Id);
        if (error != null)
        {
            Say.Info(error);
            return;
        }

        LoadAccounts();
    }

    // ---------- Backup ----------

    private void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        var save = new SaveFileDialog
        {
            Title = "Save backup",
            Filter = "Nexabode backup (*.nexabak)|*.nexabak",
            DefaultExt = ".nexabak",
            AddExtension = true,
            FileName = $"Nexabode-backup-{DateTime.Today:yyyy-MM-dd}.nexabak",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (save.ShowDialog() != true) return;

        var password = BackupPasswordDialog.Ask(Owner, "Create backup", "Create backup",
            "Choose a password for this backup file. It is separate from your sign-in password. " +
            "If the password is forgotten, the backup cannot be opened and the password cannot be recovered.",
            confirm: true);
        if (password == null) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            BackupService.CreateBackup(save.FileName, password);
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            Say.Error("The backup could not be created.\n\nDetails: " + ex.Message);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        var settings = AppSettings.Load();
        settings.LastBackup = DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture);
        settings.LastBackupAt = DateTime.Now;
        settings.Save();
        ShowLastBackup();

        Say.Info($"The backup was created and verified:\n{save.FileName}\n\n" +
                        "Keep a copy on a USB drive or cloud storage, and keep the backup password in a safe place.");
    }

    // ---------- Restore ----------

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!Session.IsOwner)
        {
            Say.Info("Only an Owner can restore a backup.");
            return;
        }

        var open = new OpenFileDialog
        {
            Title = "Choose a backup to restore",
            Filter = "Nexabode backup (*.nexabak)|*.nexabak",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (open.ShowDialog() != true) return;

        var password = BackupPasswordDialog.Ask(Owner, "Restore backup", "Continue",
            $"Enter the backup password for:\n{Path.GetFileName(open.FileName)}", confirm: false);
        if (password == null) return;

        var confirm = ConfirmActionDialog.YesNo(
            "Restoring replaces all current data (rooms, tenants, billing, payments and sign-in accounts) " +
            "with the contents of the backup.\n\n" +
            "A safety copy of your current data is saved first, protected with the same password.\n\n" +
            "Nexabode will close when the restore is complete. You will need to open it again.",
            "Confirm restore", confirmText: "Restore");
        if (confirm != MessageBoxResult.Yes) return;

        string safety;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            safety = BackupService.Restore(open.FileName, password);
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            Say.Error(ex.Message + "\n\nNothing was changed.");
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        Say.Info("The backup was restored.\n\n" +
            "Nexabode will now close. Open it again and sign in with the password that was in use when the backup was created.\n\n" +
            $"Your previous data was saved here:\n{safety}");

        MainWindow.SkipBackupPrompt = true;   // no "back up?" prompt right after a restore
        Application.Current.Shutdown();
    }

    private void CheckStays_Click(object sender, RoutedEventArgs e)
    {
        var problems = StayConflicts.ScanExisting();
        if (problems.Count == 0)
        {
            Say.Info("No overlapping or invalid stays were found.");
            return;
        }

        var more = problems.Count > 15 ? $"\n\n...and {problems.Count - 15} more." : "";
        Say.Info($"{Fmt.Count(problems.Count, "problem")} found. Nothing was changed.\n\n" +
                        string.Join("\n\n", problems.Take(15)) + more +
                        "\n\nTo correct a problem, open the tenant, select Edit this stay on one of the stays concerned, and correct its dates. " +
                        "A correction that reduces an overlap never needs an exception.");
    }
    private void ViewHistory_Click(object sender, RoutedEventArgs e) =>
    AuditHistoryWindow.ShowFor(Owner);

}