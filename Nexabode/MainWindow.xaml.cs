using Nexabode.Controls;
using Nexabode.Data;
using Nexabode.Services;
using Nexabode.Views;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
        SizeChanged += MainWindow_SizeChanged;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        // Never open larger than the screen's usable area, so nothing starts off-screen on a small display.
        var area = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, area.Width);
        MinHeight = Math.Min(MinHeight, area.Height);
        Width = Math.Min(Width, area.Width);
        Height = Math.Min(Height, area.Height);

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

    private bool _narrow;

    // On a small screen the side bar folds down to its icons, so the page gets the room. It only changes when the width
    // crosses the line, so a person who opens or closes the side bar themselves is left alone.
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = ActualWidth < 1200;
        if (narrow == _narrow) return;
        _narrow = narrow;
        RootNavigation.IsPaneOpen = !narrow;
    }

    // Ctrl+1 to Ctrl+9 and Ctrl+0 open the pages in the order of the side bar. Ctrl+F goes to the page's search box.
    // Ctrl+N does what the page's main button does.
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        var digit = e.Key is >= Key.D0 and <= Key.D9 ? e.Key - Key.D0
                  : e.Key is >= Key.NumPad0 and <= Key.NumPad9 ? e.Key - Key.NumPad0
                  : -1;
        if (digit >= 0)
        {
            var pages = RootNavigation.MenuItems.OfType<NavigationViewItem>()
                .Concat(RootNavigation.FooterMenuItems.OfType<NavigationViewItem>())
                .Select(item => item.TargetPageType)
                .Where(type => type != null)
                .ToList();
            var index = digit == 0 ? 9 : digit - 1;
            if (index < pages.Count)
            {
                NavigateTo(pages[index]!);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.F)
        {
            var box = FindDescendant<System.Windows.Controls.TextBox>(RootNavigation,
                tb => tb.Name.Contains("Search") && tb.IsVisible && tb.IsEnabled);
            if (box == null) return;
            box.Focus();
            box.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.N)
        {
            if (FindDescendant<PageHeader>(RootNavigation)?.TryInvokeAction() == true) e.Handled = true;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool>? match = null) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit && (match == null || match(hit))) return hit;
            if (FindDescendant(child, match) is { } deeper) return deeper;
        }
        return null;
    }

    // Counts what needs attention and shows it on the bell. Never blocks the app if the check fails.
    // refillPopup is false for the background refresh, so an open panel does not jump back to the top.
    public void RefreshNotifications(bool refillPopup = true)
    {
        try
        {
            using var db = AppDb.Create();
            // Optional tips are listed on the Notifications page only, so they never reach the bell or its panel.
            _notices = NotificationService.Build(db, StayQueries.Today, AppSettings.Load().ReminderLeadDays)
                .Where(n => !n.IsTip).ToList();
        }
        catch
        {
            return;
        }

        // The red count shows only what has not been read yet.
        var unread = NotificationService.UnreadCount(_notices);
        NoticeBadgeText.Text = unread > 99 ? "99+" : unread.ToString();
        NoticeBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        BellButton.ToolTip = unread == 0 ? "No new notifications" : $"{unread} new {(unread == 1 ? "notification" : "notifications")}";
        System.Windows.Automation.AutomationProperties.SetName(BellButton, BellButton.ToolTip?.ToString() ?? "Notifications");

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
            : unread == 0 ? "You are all caught up"
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
        if (!ConfirmActionDialog.Ask(this, "Log out", $"Log out of Nexabode as {Session.Administrator}?", "Log out")) return;

        NoticePopup.IsOpen = false;
        _loggingOut = true;

        new LoginWindow().Show();   // show the login window first, then close this one so the app never has no window
        Close();
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e) =>
        SwitchTheme(ThemeService.IsDark ? "Light" : "Dark");

    // Applies a new appearance ("System", "Light" or "Dark"). A new window is made so everything starts in the new theme.
    public void SwitchTheme(string choice)
    {
        // Remember which page is open so we come back to it.
        var current = (RootNavigation.SelectedItem as NavigationViewItem)?.TargetPageType
                      ?? typeof(DashboardPage);

        ThemeService.SetChoice(choice);   // saves and applies the new theme

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
            ? "You have not made a backup yet."
            : $"Your last backup was {Fmt.Count(days.Value, "day")} ago.";

        var backUp = ConfirmActionDialog.Ask(this, "Back up before closing?",
            how + "\n\nYour records live only on this computer, so a backup protects them if it breaks or is lost.",
            confirmText: "Back up now", cancelText: "Close without backup");

        if (backUp)
        {
            e.Cancel = true;
            NavigateTo(typeof(SettingsPage));
        }
    }

    public void NavigateTo(Type page) => RootNavigation.Navigate(page);
}