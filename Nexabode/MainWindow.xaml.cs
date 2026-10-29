using Nexabode.Data;
using Nexabode.Services;
using Nexabode.Views;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using static Nexabode.Views.SettingsPage;

namespace Nexabode;

public partial class MainWindow : FluentWindow
{
    private readonly Type _startPage;

    private List<Notice> _notices = new();
    private DateTime _popupClosedAt = DateTime.MinValue;

    private bool _switchingTheme;
    private bool _loggingOut;
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

        BellButton.Icon = new SymbolIcon(SymbolRegular.Alert24);

        // The sign-out icon is looked up by name, so the app still builds and runs (without the icon) if a
        // WPF-UI version names it differently.
        if (Enum.TryParse<SymbolRegular>("SignOut24", out var signOutIcon))
            LogoutButton.Icon = new SymbolIcon(signOutIcon);

        // Keep the bell's count up to date while the app stays open.
        var noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        noticeTimer.Tick += (_, _) => RefreshNotifications(false);
        noticeTimer.Start();
        Closed += (_, _) => noticeTimer.Stop();
        Activated += (_, _) => RefreshNotifications(false);

        Loaded += (_, _) =>
        {
            RootNavigation.Navigate(_startPage);
            RefreshNotifications();
        };
    }

    // Counts what needs attention and shows it on the bell. Never blocks the app if the check fails.
    // refillPopup is false for the background refresh, so an open panel does not jump back to the top.
    public void RefreshNotifications(bool refillPopup = true)
    {
        try
        {
            using var db = new AppDbContext();
            _notices = NotificationService.Build(db, StayQueries.Today, AppSettings.Load().ReminderLeadDays);
        }
        catch
        {
            return;
        }

        // The red count shows only what has not been read yet.
        var unread = NotificationService.UnreadCount(_notices);
        NoticeBadgeText.Text = unread > 99 ? "99+" : unread.ToString();
        NoticeBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        BellButton.ToolTip = unread == 0 ? "No new notifications" : $"{unread} new notification(s)";

        if (refillPopup && NoticePopup.IsOpen) FillPopup();
    }

    private void BellButton_Click(object sender, RoutedEventArgs e)
    {
        if (NoticePopup.IsOpen)
        {
            NoticePopup.IsOpen = false;
            return;
        }

        // Clicking the bell while the panel is open first closes it (it loses focus), then lands here.
        // Without this check the panel would close and straight away open again.
        if ((DateTime.UtcNow - _popupClosedAt).TotalMilliseconds < 250) return;

        RefreshNotifications();
        FillPopup();
        NoticePopup.PlacementTarget = BellButton;
        NoticePopup.HorizontalOffset = BellButton.ActualWidth - 444 - 12;   // line the panel's right edge up with the bell (12 = shadow margin)
        NoticePopup.IsOpen = true;
    }

    private void NoticePopup_Closed(object? sender, EventArgs e) => _popupClosedAt = DateTime.UtcNow;

    private void FillPopup()
    {
        var read = NotificationService.ReadKeys();
        var unread = _notices.Count(n => !read.Contains(n.Key));

        PopupSubtitle.Text = _notices.Count == 0 ? "Nothing needs your attention"
            : unread == 0 ? "You're all caught up"
            : $"{unread} new";
        MarkAllButton.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewAllButton.Content = _notices.Count > 0 ? $"View all ({_notices.Count})" : "Open Notifications";

        // New ones first, then the ones already read. The most urgent come first inside each group.
        var shown = _notices
            .OrderBy(n => read.Contains(n.Key) ? 1 : 0)
            .Take(8)
            .ToList();

        NoticePanel.Fill(PopupList, shown, read, OpenNotice, MarkNoticeRead);
    }

    private void OpenNotice(Notice notice)
    {
        NoticePopup.IsOpen = false;
        NotificationService.MarkRead(new[] { notice.Key });
        NavigateTo(NoticePanel.PageFor(notice.Target) ?? typeof(NotificationsPage));
        RefreshNotifications();
    }

    private void MarkNoticeRead(Notice notice)
    {
        NotificationService.MarkRead(new[] { notice.Key });
        RefreshNotifications();
    }

    private void MarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.MarkAllRead(_notices);
        RefreshNotifications();
    }

    private void ViewAll_Click(object sender, RoutedEventArgs e)
    {
        NoticePopup.IsOpen = false;
        NavigateTo(typeof(NotificationsPage));
    }

    // Shows the signed-in username in the card at the bottom of the menu.
    public void RefreshAccount()
    {
        AccountName.Text = Session.Administrator;
        AccountInitial.Text = UsernameRules.Initial(Session.Administrator);
        AccountRole.Text = Session.IsOwner ? "Owner" : "Staff";
    }

    // Signs out and returns to the login window. The app keeps running, so no backup prompt is shown.
    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var answer = System.Windows.MessageBox.Show(this,
            $"Log out of Nexabode as {Session.Administrator}?",
            "Log out", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (answer != System.Windows.MessageBoxResult.Yes) return;

        NoticePopup.IsOpen = false;
        _loggingOut = true;

        new LoginWindow().Show();   // show the login window first, then close this one so the app never has no window
        Close();
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
        if (_switchingTheme || _loggingOut || _sessionEnding || SkipBackupPrompt || !BackupReminder.IsDue) return;

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