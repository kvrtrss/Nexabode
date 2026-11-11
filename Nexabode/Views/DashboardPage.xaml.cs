using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Views;

public partial class DashboardPage : Page
{
    private record CalEvent(DateOnly Date, string Kind, string Title, string Detail, decimal? Amount);
    private record ActivityItem(DateOnly Date, string Title, string Detail, decimal? Amount, string Tone,
                                Wpf.Ui.Controls.SymbolRegular Icon);

    private DateOnly _month;       // first day of the month shown in the calendar
    private DateOnly _selected;    // the highlighted day
    private List<CalEvent> _events = new();

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Setup();
    }

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private void Go(Type page) => (Window.GetWindow(this) as MainWindow)?.NavigateTo(page);

    private void AddTenant_Click(object sender, RoutedEventArgs e) => Go(typeof(TenantsPage));
    private void RecordPayment_Click(object sender, RoutedEventArgs e) => Go(typeof(PaymentsPage));
    private void GenerateRent_Click(object sender, RoutedEventArgs e) => Go(typeof(RentBillingPage));
    private void AddUtility_Click(object sender, RoutedEventArgs e) => Go(typeof(UtilityBillsPage));

    // ---------- Setup reminder ----------

    // "Remind me later" lasts until the app is opened again.
    private static bool _setupHidden;

    // 'Urgent' steps are the first-time setup; the rest are gentle tips.
    private sealed record SetupStep(string Line, string Button, Func<bool> Do, bool Urgent,
                                    string? SecondaryButton = null, Action? Secondary = null);

    private SetupStep? _nextStep;

    private void ShowSetupCard()
    {
        var steps = new List<SetupStep>();

        var needRecovery = !AuthService.HasRecoveryCode(Session.UserId);
        var needEmergency = Session.IsOwner && !AuthService.HasEmergencyKey();
        if (needRecovery || needEmergency)
        {
            var line = needRecovery && needEmergency ? "Save your recovery code and emergency access key"
                     : needRecovery ? "Save your recovery code"
                     : "Save your emergency access key";
            steps.Add(new SetupStep(line, "Save my recovery codes",
                () => SetupCodesDialog.RunFirstTime(Window.GetWindow(this)), Urgent: true));
        }

        if (BackupReminder.IsDue)
        {
            var days = BackupReminder.DaysSinceBackup();
            steps.Add(days == null
                ? new SetupStep("Make your first backup", "Go to Settings", GoToSettings, Urgent: true)
                : new SetupStep($"Refresh your backup (the last one was {days} day(s) ago)", "Go to Settings",
                                GoToSettings, Urgent: false));
        }

        // The reset-phrase suggestion is only a tip, so it waits until everything else is done.
        // The second-Owner tip below then waits for this one, so only one tip shows at a time.
        if (steps.Count == 0 && Session.IsOwner && !AppSettings.Load().HideResetPhraseTip && !AuthService.HasResetPhrase())
        {
            steps.Add(new SetupStep("Optional: set a reset phrase, an easier-to-remember way back in if nobody can sign in",
                "Set a reset phrase", SetResetPhrase, Urgent: false,
                SecondaryButton: "Not needed", Secondary: () => AppSettings.Update(x => x.HideResetPhraseTip = true)));
        }

        // The second-Owner suggestion is only a tip, so it waits until everything else is done.
        if (steps.Count == 0 && Session.IsOwner && !AppSettings.Load().HideSecondOwnerTip)
        {
            using var db = new AppDbContext();
            if (AccountService.OwnerCount(db) < 2)
                steps.Add(new SetupStep("Optional: add a second Owner, so either one can help the other get back in",
                    "Add an Owner", GoToSettings, Urgent: false,
                    SecondaryButton: "Not needed", Secondary: () => AppSettings.Update(x => x.HideSecondOwnerTip = true)));
        }

        SetupRows.Children.Clear();
        _nextStep = steps.Count > 0 ? steps[0] : null;
        if (_nextStep == null || _setupHidden)
        {
            SetupCard.Visibility = Visibility.Collapsed;
            return;
        }

        var urgent = steps.Any(x => x.Urgent);
        if (urgent)
        {
            SetupCard.Background = Res("WarningSoftBrush");
            SetupCard.BorderBrush = Res("WarningBrush");
            SetupIcon.Foreground = Res("WarningBrush");
            SetupIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ShieldError24;
            SetupCard.Resources["SetupAccent"] = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09));   // deep amber, readable with white text
            SetupTitle.Text = "A few important steps are still waiting";
            SetupSubtitle.Text = "Finish setting up now for peace of mind and to keep your data safe. It only takes a few minutes.";
        }
        else
        {
            SetupCard.Background = Res("InfoSoftBrush");
            SetupCard.BorderBrush = Res("InfoBrush");
            SetupIcon.Foreground = Res("InfoBrush");
            SetupIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Shield24;
            SetupCard.Resources["SetupAccent"] = new SolidColorBrush(Color.FromRgb(0x03, 0x69, 0xA1));   // deep blue
            SetupTitle.Text = "A quick reminder";
            SetupSubtitle.Text = "Nothing is wrong. This is just a friendly nudge.";
        }

        foreach (var step in steps)
            SetupRows.Children.Add(new TextBlock
            {
                Text = "\u2022  " + step.Line,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = Res("AppTextBrush")
            });

        SetupPrimary.Content = _nextStep.Button;
        SetupSecondary.Content = _nextStep.SecondaryButton;
        SetupSecondary.Visibility = _nextStep.SecondaryButton == null ? Visibility.Collapsed : Visibility.Visible;
        SetupCard.Visibility = Visibility.Visible;
    }

    // Does the first thing still to do; a finished step drops off the list.
    private void SetupPrimary_Click(object sender, RoutedEventArgs e)
    {
        _nextStep?.Do();
        ShowSetupCard();
    }

    private void SetupSecondary_Click(object sender, RoutedEventArgs e)
    {
        _nextStep?.Secondary?.Invoke();
        ShowSetupCard();
    }

    // Asks for the password again, as Settings does, then opens the reset phrase window.
    private bool SetResetPhrase()
    {
        var owner = Window.GetWindow(this);
        if (!ConfirmPasswordDialog.Ask(owner, Session.Administrator,
                "Enter your password to set the reset phrase. The phrase can be used to regain access when nobody can sign in."))
            return false;

        return ResetPhraseDialog.Ask(owner, Session.Administrator);
    }

    private bool GoToSettings()
    {
        Go(typeof(SettingsPage));
        return false;
    }

    private void HideSetup_Click(object sender, RoutedEventArgs e)
    {
        _setupHidden = true;
        SetupCard.Visibility = Visibility.Collapsed;
    }

    private void Setup()
    {
        var now = DateTime.Now;
        var greeting = now.Hour < 12 ? "Good morning" : now.Hour < 18 ? "Good afternoon" : "Good evening";
        Header.Subtitle = $"{greeting}. {now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)}";

        var today = DateOnly.FromDateTime(DateTime.Today);
        _month = new DateOnly(today.Year, today.Month, 1);
        _selected = today;

        LoadStats();
        ShowSetupCard();
        LoadActivity();
        BuildCalendar();
    }

    // ---------- Stat cards ----------

    private void LoadStats()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var lastStart = monthStart.AddMonths(-1);

        using var db = new AppDbContext();

        var totalBeds = db.Beds.Count(b => b.IsActive && b.Room.IsActive && !b.IsMaintenance);
        var occupied = db.Occupancies.CurrentOrUpcoming().Select(o => o.BedId).Distinct().Count();
        var vacant = Math.Max(0, totalBeds - occupied);
        var rate = totalBeds == 0 ? 0 : occupied * 100.0 / totalBeds;

        // Never show 100% while a bed is still vacant.
        var shownRate = occupied >= totalBeds
            ? rate
            : Math.Min(99, Math.Round(rate, MidpointRounding.AwayFromZero));

        OccupancyCard.Value = $"{shownRate:0}%";
        OccupancyCard.Caption = totalBeds == 0 ? "Add rooms and beds to begin" : "of active beds, including reserved";
        OccupancyCard.Tone = "Info";

        BedsCard.Value = $"{occupied} / {totalBeds}";
        BedsCard.Caption = $"{vacant} bed(s) available";
        BedsCard.Tone = "Success";

        // Money is summed in C# because SQLite can't add up decimal columns.
        var payments = db.Payments.Where(p => p.PaymentDate >= lastStart && !p.IsVoided).ToList();
        var thisMonth = payments.Where(p => p.PaymentDate >= monthStart).Sum(p => p.Amount);
        var lastMonth = payments.Where(p => p.PaymentDate < monthStart).Sum(p => p.Amount);

        CollectedCard.Value = Fmt.Peso(thisMonth);
        if (lastMonth <= 0)
        {
            CollectedCard.Caption = thisMonth > 0 ? "No payments last month" : "No payments yet";
            CollectedCard.Tone = "Neutral";
        }
        else
        {
            var change = (double)((thisMonth - lastMonth) / lastMonth * 100);
            CollectedCard.Caption = $"{(change >= 0 ? "↑" : "↓")} {Math.Abs(change):0}% vs last month";
            CollectedCard.Tone = change >= 0 ? "Success" : "Warning";
        }

        var charges = db.Charges.Include(c => c.Allocations).ToList();
        var openBalances = charges
            .Select(c => new { c.DueDate, Balance = c.Amount - c.Allocations.Sum(a => a.AmountApplied) })
            .Where(x => x.Balance > 0)
            .ToList();
        var overdueCount = openBalances.Count(x => x.DueDate < today);

        OutstandingCard.Value = Fmt.Peso(openBalances.Sum(x => x.Balance));
        OutstandingCard.Caption = overdueCount > 0 ? $"{overdueCount} overdue charge(s)" : "Nothing overdue";
        OutstandingCard.Tone = overdueCount > 0 ? "Danger" : "Success";
    }

    // ---------- Calendar ----------

    private void PrevMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(_month.AddMonths(-1));
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(_month.AddMonths(1));

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _selected = today;
        ChangeMonth(new DateOnly(today.Year, today.Month, 1));
    }

    private void ChangeMonth(DateOnly newMonth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _month = newMonth;
        _selected = (today.Year == newMonth.Year && today.Month == newMonth.Month) ? today : newMonth;
        BuildCalendar();
    }

    private void BuildCalendar()
    {
        var last = _month.AddMonths(1).AddDays(-1);
        MonthTitle.Text = _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        _events = LoadMonthEvents(_month, last);
        RenderDays();
        ShowDay(_selected);
    }

    private List<CalEvent> LoadMonthEvents(DateOnly first, DateOnly last)
    {
        var events = new List<CalEvent>();
        using var db = new AppDbContext();

        var charges = db.Charges
    .Include(c => c.Allocations)
    .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
    .Where(c => c.DueDate >= first && c.DueDate <= last)
    .ToList();
        foreach (var c in charges)
        {
            var balance = c.Amount - c.Allocations.Sum(a => a.AmountApplied);
            events.Add(new CalEvent(
                c.DueDate,
                c.ChargeType == ChargeType.Rent ? "Rent due" : "Utility",
                c.Occupancy.Tenant.FullName,
                $"{c.Description ?? c.ChargeType.ToString()}, " + (balance <= 0 ? "fully paid" : $"{Fmt.Peso(balance)} remaining"),
                c.Amount));
        }

        var payments = db.Payments
    .Include(p => p.Tenant)
    .Where(p => p.PaymentDate >= first && p.PaymentDate <= last && !p.IsVoided)
    .ToList();
        foreach (var p in payments)
            events.Add(new CalEvent(p.PaymentDate, "Payment", p.Tenant.FullName,
                p.Method ?? "Payment received", p.Amount));

        var stays = db.Occupancies
            .Include(o => o.Tenant)
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .ToList();
        foreach (var s in stays)
        {
            var bed = $"{s.Bed.Room.RoomName} / {s.Bed.BedLabel}";
            if (s.MoveInDate >= first && s.MoveInDate <= last)
                events.Add(new CalEvent(s.MoveInDate, "Move", s.Tenant.FullName, $"Moves in to {bed}", null));
            if (s.MoveOutDate is DateOnly mo && mo >= first && mo <= last)
                events.Add(new CalEvent(mo, "Move", s.Tenant.FullName, $"Moves out of {bed}", null));
        }

        return events;
    }

    private static string KindKey(string kind) => kind switch
    {
        "Rent due" => "Warning",
        "Payment" => "Success",
        "Utility" => "Info",
        _ => "Occupied"
    };

    private void RenderDays()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        DayGrid.Children.Clear();

        var offset = ((int)_month.DayOfWeek + 6) % 7;   // Monday-first
        for (var i = 0; i < offset; i++)
            DayGrid.Children.Add(new Border { Height = 54 });

        var days = DateTime.DaysInMonth(_month.Year, _month.Month);
        for (var d = 1; d <= days; d++)
        {
            var date = new DateOnly(_month.Year, _month.Month, d);
            var kinds = _events.Where(e => e.Date == date).Select(e => e.Kind).ToList();
            DayGrid.Children.Add(MakeDayCell(date, date == today, date == _selected, kinds));
        }
    }

    private Border MakeDayCell(DateOnly date, bool isToday, bool isSelected, List<string> kinds)
    {
        var number = new TextBlock
        {
            Text = date.Day.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = kinds.Count > 0 ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = isToday ? Brushes.White : Res("AppTextBrush")
        };

        var circle = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = isToday ? Res("AppPrimaryBrush")
                       : isSelected ? Res("AppBorderBrush")
                       : Brushes.Transparent,
            Child = number
        };

        var dots = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0),
            Height = 4
        };
        foreach (var kind in kinds.Distinct())
            dots.Children.Add(new Border
            {
                Width = 12,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(1, 0, 1, 0),
                Background = Res(KindKey(kind) + "Brush")
            });

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(circle);
        stack.Children.Add(dots);

        var cell = new Border
        {
            Height = 54,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = stack
        };
        cell.MouseLeftButtonUp += (_, _) =>
        {
            _selected = date;
            RenderDays();
            ShowDay(date);
        };
        return cell;
    }

    private void ShowDay(DateOnly date)
    {
        DayDetailTitle.Text = date.ToString("dddd, MMMM d", CultureInfo.CurrentCulture);
        DayDetailList.Children.Clear();

        var items = _events.Where(e => e.Date == date).ToList();
        if (items.Count == 0)
        {
            DayDetailList.Children.Add(new TextBlock
            {
                Text = "Nothing scheduled on this day.",
                Style = (Style)FindResource("MutedText")
            });
            return;
        }

        foreach (var item in items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var bar = new Border
            {
                Width = 4,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 10, 0),
                Background = Res(KindKey(item.Kind) + "Brush")
            };

            var text = new StackPanel();
            text.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("AppTextBrush")
            });
            text.Children.Add(new TextBlock
            {
                Text = $"{item.Kind}: {item.Detail}",
                Style = (Style)FindResource("MutedText")
            });
            Grid.SetColumn(text, 1);

            row.Children.Add(bar);
            row.Children.Add(text);

            if (item.Amount is decimal amount)
            {
                var money = new TextBlock
                {
                    Text = Fmt.Peso(amount),
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Res("AppTextBrush")
                };
                Grid.SetColumn(money, 2);
                row.Children.Add(money);
            }

            DayDetailList.Children.Add(row);
        }
    }

    // ---------- Recent activity ----------

    private void LoadActivity()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cutoff = today.AddDays(-45);
        var items = new List<ActivityItem>();

        using var db = new AppDbContext();

        foreach (var p in db.Payments.Include(p => p.Tenant).ToList().Where(p => p.PaymentDate >= cutoff && !p.IsVoided))
            items.Add(new ActivityItem(p.PaymentDate, "PAYMENT RECEIVED",
                p.Tenant.FullName + (string.IsNullOrWhiteSpace(p.Method) ? "" : $" ({p.Method})"),
                p.Amount, "Success", Wpf.Ui.Controls.SymbolRegular.Money24));

        var stays = db.Occupancies
            .Include(o => o.Tenant)
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .ToList();
        foreach (var s in stays)
        {
            var bed = $"{s.Bed.Room.RoomName} / {s.Bed.BedLabel}";
            if (s.MoveInDate >= cutoff && s.MoveInDate <= today)
                items.Add(new ActivityItem(s.MoveInDate, "TENANT MOVED IN", $"{s.Tenant.FullName}, {bed}", null, "Occupied",
                    Wpf.Ui.Controls.SymbolRegular.PersonArrowRight24));
            if (s.MoveOutDate is DateOnly mo && mo >= cutoff && mo <= today)
                items.Add(new ActivityItem(mo, "TENANT MOVED OUT", $"{s.Tenant.FullName}, {bed}", null, "Neutral",
                    Wpf.Ui.Controls.SymbolRegular.PersonArrowLeft24));
        }

        var overdue = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .ToList()
            .Select(c => new { Charge = c, Balance = c.Amount - c.Allocations.Sum(a => a.AmountApplied) })
            .Where(x => x.Charge.DueDate < today && x.Balance > 0)
            .OrderByDescending(x => x.Charge.DueDate)
            .Take(3);
        foreach (var x in overdue)
        {
            var late = today.DayNumber - x.Charge.DueDate.DayNumber;
            items.Add(new ActivityItem(x.Charge.DueDate, "CHARGE OVERDUE",
                $"{x.Charge.Occupancy.Tenant.FullName}, {late} day(s) overdue", x.Balance, "Danger",
                Wpf.Ui.Controls.SymbolRegular.Warning24));
        }

        ActivityList.Children.Clear();
        var latest = items.OrderByDescending(i => i.Date).Take(7).ToList();

        if (latest.Count == 0)
        {
            ActivityList.Children.Add(new TextBlock
            {
                Text = "No activity yet. Add a tenant or record a payment to get started.",
                Style = (Style)FindResource("MutedText"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var item in latest)
            ActivityList.Children.Add(MakeActivityRow(item));
    }

    private UIElement MakeActivityRow(ActivityItem item)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 12, 0),
            Background = Res(item.Tone + "SoftBrush"),
            // The icon says what happened; its colour (matching the title) says how it counts: green for money in,
            // indigo for a move, grey for a move-out, red for something overdue.
            Child = new Wpf.Ui.Controls.SymbolIcon(item.Icon, 18)
            {
                Foreground = Res(item.Tone + "Brush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res(item.Tone + "Brush")
        });
        text.Children.Add(new TextBlock
        {
            Text = item.Detail,
            Style = (Style)FindResource("MutedText"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        text.Children.Add(new TextBlock
        {
            Text = Fmt.Date(item.Date),
            Style = (Style)FindResource("MutedText"),
            FontSize = 11
        });
        Grid.SetColumn(text, 1);

        row.Children.Add(icon);
        row.Children.Add(text);

        if (item.Amount is decimal amount)
        {
            var money = new TextBlock
            {
                Text = Fmt.Peso(amount),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = Res("AppTextBrush")
            };
            Grid.SetColumn(money, 2);
            row.Children.Add(money);
        }

        return row;
    }
}