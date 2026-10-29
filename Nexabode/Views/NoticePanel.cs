using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Nexabode.Services;

namespace Nexabode.Views;

// Draws the notification list inside the bell's drop-down panel.
public static class NoticePanel
{
    public static Type? PageFor(NoticeTarget target) => target switch
    {
        NoticeTarget.Tenants => typeof(TenantsPage),
        NoticeTarget.RentBilling => typeof(RentBillingPage),
        NoticeTarget.UtilityBills => typeof(UtilityBillsPage),
        NoticeTarget.BillingCharges => typeof(BillingPage),
        NoticeTarget.Payments => typeof(PaymentsPage),
        NoticeTarget.Settings => typeof(SettingsPage),
        _ => null
    };

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private static Style? MutedStyle => Application.Current.TryFindResource("MutedText") as Style;

    // open: the row was clicked. markRead: its X was clicked.
    public static void Fill(StackPanel host, IReadOnlyList<Notice> notices, ISet<string> read,
                            Action<Notice> open, Action<Notice> markRead)
    {
        host.Children.Clear();

        if (notices.Count == 0)
        {
            host.Children.Add(new TextBlock
            {
                Text = "You're all caught up. Nothing is overdue or due soon.",
                Style = MutedStyle,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 18, 16, 18)
            });
            return;
        }

        foreach (var n in notices)
            host.Children.Add(MakeItem(n, read.Contains(n.Key), open, markRead));
    }

    private static UIElement MakeItem(Notice n, bool isRead, Action<Notice> open, Action<Notice> markRead)
    {
        var grid = new Grid
        {
            Margin = new Thickness(16, 10, 10, 10),
            Background = Brushes.Transparent,   // so the whole row can be clicked
            Cursor = Cursors.Hand
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Margin = new Thickness(0, 6, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Fill = Res(n.Tone + "Brush"),
            Opacity = isRead ? 0.3 : 1
        };
        Grid.SetColumn(dot, 0);
        grid.Children.Add(dot);

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = n.Category,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res(n.Tone + "Brush"),
            Opacity = isRead ? 0.5 : 1
        });
        text.Children.Add(new TextBlock
        {
            Text = n.Title,
            FontWeight = isRead ? FontWeights.Normal : FontWeights.SemiBold,
            Foreground = Res(isRead ? "AppSecondaryBrush" : "AppTextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2)
        });
        if (n.Detail != "")
        {
            text.Children.Add(new TextBlock
            {
                Text = n.Detail,
                Style = MutedStyle,
                TextWrapping = TextWrapping.Wrap
            });
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (!isRead)
        {
            var dismiss = new Wpf.Ui.Controls.Button
            {
                Content = "\u2715",
                ToolTip = "Mark as read",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(8, 4, 8, 4)
            };
            dismiss.Click += (_, e) =>
            {
                e.Handled = true;
                markRead(n);
            };
            Grid.SetColumn(dismiss, 2);
            grid.Children.Add(dismiss);
        }

        grid.MouseLeftButtonUp += (_, e) =>
        {
            if (!e.Handled) open(n);
        };

        return new Border
        {
            BorderBrush = Res("AppBorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }
}