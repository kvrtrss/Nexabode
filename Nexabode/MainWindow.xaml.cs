using System.Windows;
using Nexabode.Services;
using Nexabode.Views;
using Wpf.Ui.Controls;

namespace Nexabode;

public partial class MainWindow : FluentWindow
{
    private readonly Type _startPage;

    private bool _switchingTheme;
    private static bool _sessionEnding;

    // Set before a deliberate shutdown (for example after a restore) so no backup prompt appears.
    public static bool SkipBackupPrompt { get; set; }

    static MainWindow()
    {
        if (Application.Current != null)
            Application.Current.SessionEnding += (_, _) => _sessionEnding = true;
    }

    public MainWindow() : this(typeof(DashboardPage)) { }

    public MainWindow(Type startPage)
    {
        InitializeComponent();
        RefreshAccount();
        _startPage = startPage;
        Closing += MainWindow_Closing;

        ThemeButton.Icon = new SymbolIcon(ThemeService.IsDark
            ? SymbolRegular.WeatherSunny24
            : SymbolRegular.WeatherMoon24);

        Loaded += (_, _) => RootNavigation.Navigate(_startPage);
    }

    // Shows the signed-in username in the card at the bottom of the menu.
    public void RefreshAccount()
    {
        AccountName.Text = Session.Administrator;
        AccountInitial.Text = UsernameRules.Initial(Session.Administrator);
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        // Remember which page is open so we come back to it.
        var current = (RootNavigation.SelectedItem as NavigationViewItem)?.TargetPageType
                      ?? typeof(DashboardPage);

        ThemeService.Toggle();            // saves and applies the new theme

        var fresh = new MainWindow(current);   // a new window starts in the new theme -

        // Put the new window exactly where this one was, with the same size and state.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        fresh.WindowStartupLocation = WindowStartupLocation.Manual;
        fresh.Left = bounds.Left;
        fresh.Top = bounds.Top;
        fresh.Width = bounds.Width;
        fresh.Height = bounds.Height;
        fresh.WindowState = WindowState;

        fresh.Show();
        _switchingTheme = true;
        Close();

    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_switchingTheme || _sessionEnding || SkipBackupPrompt || !BackupReminder.IsDue) return;

        var days = BackupReminder.DaysSinceBackup();
        var how = days == null
            ? "You haven't made a backup yet."
            : $"Your last backup was {days} day(s) ago.";

        var answer = System.Windows.MessageBox.Show(this,
            how + "\n\nBack up before closing?\n\n" +
            "Yes opens Settings so you can create a backup. No closes Nexabode now.",
            "Nexabode", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

        if (answer == System.Windows.MessageBoxResult.Yes)
        {
            e.Cancel = true;
            NavigateTo(typeof(SettingsPage));
        }
    }

    public void NavigateTo(Type page) => RootNavigation.Navigate(page);
}