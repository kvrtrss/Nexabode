using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class NotificationsPage : Page
{
    private static readonly int[] LeadOptions = { 1, 2, 3, 5, 7, 14, 30 };

    private bool _ready;
    private List<Notice> _notices = new();

    public NotificationsPage()
    {
        InitializeComponent();

        foreach (var days in LeadOptions)
            LeadCombo.Items.Add(new ComboBoxItem { Content = days == 1 ? "1 day" : $"{days} days", Tag = days });

        var saved = NotificationService.ClampLeadDays(AppSettings.Load().ReminderLeadDays);
        var index = Array.IndexOf(LeadOptions, saved);
        LeadCombo.SelectedIndex = index >= 0 ? index : Array.IndexOf(LeadOptions, NotificationService.DefaultLeadDays);

        Loaded += (_, _) =>
        {
            _ready = true;
            LoadAll();
        };
    }

    // ---------- Helpers ----------

    private Window? Owner => Window.GetWindow(this);

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);


    private int LeadDays =>
        (LeadCombo.SelectedItem as ComboBoxItem)?.Tag is int days ? days : NotificationService.DefaultLeadDays;

    private static Type? PageFor(NoticeTarget target) => NoticePanel.PageFor(target);

    // ---------- Loading ----------

    private void LoadAll()
    {
        var today = StayQueries.Today;

        List<Notice> notices;
        List<Reminder> reminders;
        using (var db = AppDb.Create())
        {
            notices = NotificationService.Build(db, today, LeadDays);
            reminders = NotificationService.OpenReminders(db);
        }

        // Real alerts drive the bell. Optional tips are listed separately and never counted.
        var alerts = notices.Where(n => !n.IsTip).ToList();
        var tips = notices.Where(n => n.IsTip).ToList();

        _notices = alerts;
        var read = NotificationService.ReadKeys();
        var unread = alerts.Count(n => !read.Contains(n.Key));

        NoticeList.Children.Clear();
        NoticeTitle.Text = alerts.Count == 0 ? "Needs your attention"
            : "Needs your attention: " + Fmt.Count(alerts.Count, "item")
              + (unread == 0 ? ", all read" : unread == alerts.Count ? "" : $", {unread} new");

        if (alerts.Count == 0)
        {
            NoticeList.Children.Add(new TextBlock
            {
                Text = "You are all caught up. Nothing is overdue or due soon.",
                Style = (Style)FindResource("MutedText"),
                Margin = new Thickness(0, 0, 0, 8)
            });
        }
        foreach (var n in alerts)
            NoticeList.Children.Add(MakeNoticeRow(n, read.Contains(n.Key)));

        ReminderList.Children.Clear();
        if (reminders.Count == 0)
        {
            ReminderList.Children.Add(new TextBlock
            {
                Text = "No reminders yet. Select Add reminder for things Nexabode cannot track on its own, such as a meter reading or a repair visit.",
                Style = (Style)FindResource("MutedText"),
                TextWrapping = TextWrapping.Wrap
            });
        }
        foreach (var r in reminders)
            ReminderList.Children.Add(MakeReminderRow(r, today));

        TipList.Children.Clear();
        TipTitle.Visibility = tips.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tip in tips)
            TipList.Children.Add(MakeNoticeRow(tip, false));

        (Window.GetWindow(this) as MainWindow)?.RefreshNotifications();
    }

    // What the Open button says, so it is clear where it leads.
    public static string OpenLabel(Notice n) => n.OpenLabel != "" ? n.OpenLabel : n.Target switch
    {
        NoticeTarget.Tenants => "View tenants",
        NoticeTarget.RentBilling => "Open Rent Billing",
        NoticeTarget.UtilityBills => "Open Utility Bills",
        NoticeTarget.BillingCharges => "View charges",
        NoticeTarget.Payments => "Open Payments",
        NoticeTarget.Settings => "Open Settings",
        _ => "Open"
    };

    // ---------- Rows ----------

    private Wpf.Ui.Controls.Button MakeButton(string text, RoutedEventHandler click)
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Content = text,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += click;
        return button;
    }

    private Border MakeRow(string tone, string category, string title, string detail, bool dim, params Wpf.Ui.Controls.Button[] buttons)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stripe = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 14, 0),
            Background = Res(tone + "Brush")
        };
        Grid.SetColumn(stripe, 0);
        grid.Children.Add(stripe);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = category,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res(tone + "Brush")
        });
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("AppTextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2)
        });
        if (detail != "")
        {
            text.Children.Add(new TextBlock
            {
                Text = detail,
                Style = (Style)FindResource("MutedText"),
                TextWrapping = TextWrapping.Wrap
            });
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var button in buttons) actions.Children.Add(button);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        return new Border
        {
            Background = Res("AppSurfaceBrush"),
            BorderBrush = Res("AppBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = dim ? 0.6 : 1,   // a notification that was already read is shown fainter
            Child = grid
        };
    }

    private Border MakeNoticeRow(Notice n, bool isRead)
    {
        var buttons = new List<Wpf.Ui.Controls.Button>();

        if (n.ReminderId is int reminderId)
        {
            buttons.Add(MakeButton("Done", (_, _) => MarkDone(reminderId)));
            buttons.Add(MakeButton("Delete", (_, _) => DeleteReminder(reminderId, n.Title)));
        }
        else if (PageFor(n.Target) is Type page)
        {
            buttons.Add(MakeButton(OpenLabel(n), (_, _) => (Window.GetWindow(this) as MainWindow)?.NavigateTo(page)));
        }

        if (n.Dismiss is { } dismiss)
            buttons.Add(MakeButton("Hide tip", (_, _) => { dismiss(); LoadAll(); }));

        return MakeRow(n.Tone, n.Category, n.Title, n.Detail, isRead, buttons.ToArray());
    }

    private Border MakeReminderRow(Reminder r, DateOnly today)
    {
        var tone = r.DueDate < today ? "Danger" : r.DueDate == today ? "Warning" : "Info";
        var when = r.DueDate < today ? $"Was due {Fmt.Date(r.DueDate)}"
            : r.DueDate == today ? "Due today"
            : $"Due {Fmt.Date(r.DueDate)}";
        var detail = string.IsNullOrWhiteSpace(r.Note) ? when : $"{when}. {r.Note!.Trim()}";

        return MakeRow(tone, "Reminder", r.Title, detail, false,
            MakeButton("Done", (_, _) => MarkDone(r.Id)),
            MakeButton("Delete", (_, _) => DeleteReminder(r.Id, r.Title)));
    }

    // ---------- Actions ----------

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        var today = StayQueries.Today;

        var v = FormDialog.Ask(Owner, "Add reminder", "Add reminder",
            new[]
            {
                ("What should Nexabode remind you about?", ""),
                ("Remind me on (e.g. 2026-10-30)", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("Note", "")
            },
            vals =>
            {
                if (vals[0].Trim() == "") return "Enter what the reminder is about.";
                if (vals[0].Trim().Length > 120) return "Keep the reminder under 120 characters. Use the note for details.";
                if (!DateOnly.TryParse(vals[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out _))
                    return "Enter the date in this format: 2026-10-30.";
                return null;
            });
        if (v == null) return;

        DateOnly.TryParse(v[1].Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var due);

        using (var db = AppDb.Create())
        {
            db.Reminders.Add(new Reminder
            {
                Title = v[0].Trim(),
                DueDate = due,
                Note = string.IsNullOrWhiteSpace(v[2]) ? null : v[2].Trim()
            });
            db.SaveChanges();
        }

        LoadAll();
    }

    private void MarkDone(int reminderId)
    {
        using (var db = AppDb.Create())
        {
            var reminder = db.Reminders.Find(reminderId);
            if (reminder != null)
            {
                reminder.IsDone = true;
                db.SaveChanges();
            }
        }

        LoadAll();
    }

    private void DeleteReminder(int reminderId, string title)
    {
        var confirm = ConfirmActionDialog.YesNo($"Delete the reminder \"{title}\"?", "Delete reminder", confirmText: "Delete");
        if (confirm != MessageBoxResult.Yes) return;

        using (var db = AppDb.Create())
        {
            var reminder = db.Reminders.Find(reminderId);
            if (reminder != null)
            {
                db.Reminders.Remove(reminder);
                db.SaveChanges();
            }
        }

        LoadAll();
    }

    private void LeadCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;

        var days = NotificationService.ClampLeadDays(LeadDays);
        AppSettings.Update(s => s.ReminderLeadDays = days);

        LoadAll();
    }

    private void MarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        NotificationService.MarkAllRead(_notices);
        LoadAll();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadAll();
}