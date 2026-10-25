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

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private Window? Owner => Window.GetWindow(this);

    private void Load()
    {
        using var db = new AppDbContext();
        _username = db.AdminUsers.OrderBy(u => u.Id).Select(u => u.Username).FirstOrDefault() ?? "admin";

        AccountText.Text = $"Signed in as {_username}. Use a password only you know.";
        DataPathText.Text = DatabaseKeyProvider.AppFolder;
        VersionText.Text = $"Version {typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)}";
        ShowLastBackup();
        RulesText.Text = $"Data safety rules active: {DbInitializer.CountActiveSafetyRules(db)} of {DbInitializer.SafetyRuleCount}";
    }

    private void ShowLastBackup()
    {
        var settings = AppSettings.Load();
        var last = BackupReminder.LastBackupTime();

        if (last == null && string.IsNullOrWhiteSpace(settings.LastBackup))
        {
            LastBackupText.Text = "No backup yet. Create one before you enter real data.";
            LastBackupText.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
        }
        else if (last == null)
        {
            LastBackupText.Text = $"Last backup: {settings.LastBackup}. Create a new one to start the backup reminder.";
            LastBackupText.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
        }
        else
        {
            var days = BackupReminder.DaysSinceBackup();
            var age = days == 0 ? "today" : $"{days} day(s) ago";
            LastBackupText.Text = $"Last backup: {last.Value.ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)} ({age})";
            LastBackupText.Foreground = (System.Windows.Media.Brush)FindResource(
                BackupReminder.IsDue ? "WarningBrush" : "SuccessBrush");
        }
    }

    // ---------- Security ----------

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (ChangePasswordDialog.Ask(Owner, _username, forced: false))
            MessageBox.Show("Your password has been changed.", "Nexabode",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ChangeUsername_Click(object sender, RoutedEventArgs e)
    {
        if (!ChangeUsernameDialog.Ask(Owner, _username, out var newName)) return;

        _username = newName;
        AccountText.Text = $"Signed in as {_username}. Use a password only you know.";
        (Window.GetWindow(this) as MainWindow)?.RefreshAccount();

        MessageBox.Show($"Your username is now {newName}. Use it the next time you sign in.", "Nexabode",
            MessageBoxButton.OK, MessageBoxImage.Information);
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
            "If you forget it, the backup cannot be opened and there is no way to recover it.",
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
            MessageBox.Show("The backup could not be created.\n\n" + ex.Message, "Nexabode",
                MessageBoxButton.OK, MessageBoxImage.Error);
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

        MessageBox.Show($"Backup created and verified:\n{save.FileName}\n\n" +
                        "Keep a copy on a USB drive or cloud storage, and remember the backup password.",
                        "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---------- Restore ----------

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
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

        var confirm = MessageBox.Show(
            "Restoring replaces ALL current data (rooms, tenants, billing, payments, and the sign-in password) " +
            "with the contents of the backup.\n\n" +
            "A safety copy of your current data is saved first, locked with the same password.\n\n" +
            "Nexabode will close when the restore finishes, and you open it again.\n\nContinue?",
            "Confirm restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
            MessageBox.Show(ex.Message + "\n\nNothing was changed.", "Nexabode",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        MessageBox.Show(
            "The backup was restored.\n\n" +
            "Nexabode will now close. Open it again and sign in with the password that was in use when the backup was made.\n\n" +
            $"Your previous data was saved here:\n{safety}",
            "Nexabode", MessageBoxButton.OK, MessageBoxImage.Information);

        MainWindow.SkipBackupPrompt = true;   // no "back up?" prompt right after a restore
        Application.Current.Shutdown();
    }

    private void CheckStays_Click(object sender, RoutedEventArgs e)
    {
        var problems = StayConflicts.ScanExisting();
        if (problems.Count == 0)
        {
            MessageBox.Show("No overlapping or invalid stays were found.", "Nexabode",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var more = problems.Count > 15 ? $"\n\n...and {problems.Count - 15} more." : "";
        MessageBox.Show($"{problems.Count} problem(s) found. Nothing was changed.\n\n" +
                        string.Join("\n\n", problems.Take(15)) + more +
                        "\n\nTo fix one: open the tenant, click Edit this stay on one of the stays, and correct its dates. " +
                        "A correction that makes an overlap smaller never needs an exception.",
            "Nexabode", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void ViewHistory_Click(object sender, RoutedEventArgs e) =>
    AuditHistoryWindow.ShowFor(Owner);

}