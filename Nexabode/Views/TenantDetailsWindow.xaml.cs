using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Nexabode.Services;

namespace Nexabode.Views;

// Everything about one tenant, in its own window: details, the actions (tenant, stay and deposit) and the stay history.
// It used to be a permanent panel beside the list; as a window, the list keeps the whole page width.
// The actions themselves still live on the Tenants page and are only called from here.
public partial class TenantDetailsWindow : Window
{
    private readonly TenantsPage _page;
    private readonly int _tenantId;

    public TenantDetailsWindow(TenantsPage page, int tenantId)
    {
        InitializeComponent();
        _page = page;
        _tenantId = tenantId;
        Loaded += (_, _) => Refresh();
    }

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    // Fills the window from the tenant's current data. Called when it opens and after every action.
    public void Refresh()
    {
        var t = _page.RowFor(_tenantId);
        if (t == null || _page.CurrentId != _tenantId)
        {
            Close();   // the tenant is no longer in the list (for example, filtered out after moving out)
            return;
        }

        DetailName.Text = t.FullName;
        DetailBadge.Content = t.Status;

        InfoPanel.Children.Clear();
        AddLine("Gender", GenderRules.Display(t.Gender));
        AddLine("Contact", t.Contact ?? "-");
        AddLine("Emergency", t.EmergencyContact ?? "-");
        AddLine("School / work", t.SchoolOrWork ?? "-");
        AddLine("Current bed", t.CurrentBed);
        AddLine("Move-in", t.MoveInText);
        if (t.LeavingOn is DateOnly leavingOn) AddLine("Last day", Fmt.Date(leavingOn), "Warning");
        AddLine("Balance", Fmt.Peso(t.Balance), t.Overdue > 0 ? "Danger" : t.Balance > 0 ? "Warning" : "Success");
        if (t.Overdue > 0) AddLine("Overdue", Fmt.Peso(t.Overdue), "Danger");
        AddLine("Deposit balance", Fmt.Peso(t.Deposit.Held), t.Deposit.Held > 0 ? "Success" : null);
        if (t.Deposit.Applied > 0) AddLine("Deposit used", Fmt.Peso(t.Deposit.Applied));
        if (t.Deposit.Refunded > 0) AddLine("Deposit refunded", Fmt.Peso(t.Deposit.Refunded));
        // Two of this tenant's stays on the same days: point it out so it can be corrected or removed.
        var overlapping = t.Stays
            .SelectMany(a => t.Stays
                .Where(b => a.Id < b.Id &&
                            StayRules.Overlaps(new StayRange(a.Id, a.MoveIn, a.MoveOut), new StayRange(b.Id, b.MoveIn, b.MoveOut)))
                .Select(b => (A: a, B: b)))
            .ToList();
        if (overlapping.Count > 0)
        {
            var duplicate = overlapping.Any(p => StayRemovalRules.IsDuplicate(
                p.A.Bed, p.A.MoveIn, p.A.MoveOut, p.B.Bed, p.B.MoveIn, p.B.MoveOut));
            AddLine("Check", StayRemovalRules.OverlapWarning(duplicate, Session.IsOwner), "Danger");
        }

        // Someone else is booked on the same bed on some of the same days.
        foreach (var bed in t.Stays.Where(s => s.SharedWith.Count > 0).GroupBy(s => s.Bed))
        {
            var others = bed.SelectMany(s => s.SharedWith).Distinct().OrderBy(n => n).ToList();
            AddLine("Check", ClashWarnings.TenantBedWarning(bed.Key, others, Session.IsOwner), "Danger");
        }
        if (!string.IsNullOrWhiteSpace(t.Notes)) AddLine("Notes", t.Notes!);

        MoveInButton.IsEnabled = t.Status is "Moved out" or "No stay yet";
        MoveOutButton.IsEnabled = t.Status is "Staying" or "Moving out" or "Reserved";
        MoveOutButton.Content = t.Status == "Reserved" ? "Cancel reservation" : "Move out";
        EditStayButton.IsEnabled = t.Stays.Count > 0;
        RefundDepositButton.IsEnabled = t.Deposit.Held > 0;
        ApplyDepositButton.IsEnabled = t.Deposit.Held > 0;
        EditDepositButton.IsEnabled = t.Stays.Count > 0;           // deposit is kept on a stay
        UndoRefundButton.IsEnabled = t.Deposit.Refunded > 0;

        HistoryPanel.Children.Clear();
        if (t.Stays.Count == 0 && t.Cancelled.Count == 0)
        {
            HistoryPanel.Children.Add(new TextBlock
            {
                Text = "No stays recorded yet.",
                Style = (Style)FindResource("MutedText")
            });
            return;
        }

        foreach (var stay in t.Stays)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            box.Children.Add(new TextBlock
            {
                Text = stay.Bed,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("AppTextBrush")
            });
            var details = $"{Fmt.Date(stay.MoveIn)} to {(stay.MoveOut is DateOnly mo ? Fmt.Date(mo) : "present")}  -  {Fmt.Peso(stay.Rent)} / month";
            if (stay.Deposit > 0)
            {
                details += $"  -  Deposit {Fmt.Peso(stay.Deposit)}";
                if (stay.DepositRefunded > 0)
                    details += $" (refunded {Fmt.Peso(stay.DepositRefunded)}" +
                               (stay.DepositRefundedOn is DateOnly refundedOn ? $" on {Fmt.Date(refundedOn)}" : "") + ")";
            }
            box.Children.Add(new TextBlock
            {
                Text = details,
                Style = (Style)FindResource("MutedText")
            });

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

            var editLink = new Wpf.Ui.Controls.Button { Content = "Edit this stay" };
            editLink.Click += (_, _) => _page.EditStay(stay.Id);
            actions.Children.Add(editLink);

            // Only an Owner can remove a stay, and only one that was never billed.
            if (Session.IsOwner)
            {
                var removeLink = new Wpf.Ui.Controls.Button
                {
                    Content = "Remove this stay",
                    Style = (Style)Application.Current.FindResource("DangerButton"),
                    Margin = new Thickness(8, 0, 0, 0),
                    ToolTip = "For a stay entered by mistake. Deleted if it was never billed, otherwise kept as Cancelled."
                };
                removeLink.Click += (_, _) => _page.RemoveStay(stay.Id);
                actions.Children.Add(removeLink);
            }

            box.Children.Add(actions);

            HistoryPanel.Children.Add(box);
        }

        // Stays that were removed after being billed: kept for the records, greyed, with no buttons.
        foreach (var cancelled in t.Cancelled)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 8), Opacity = 0.7 };
            box.Children.Add(new TextBlock
            {
                Text = cancelled.Bed,
                FontWeight = FontWeights.SemiBold,
                TextDecorations = TextDecorations.Strikethrough,
                Foreground = Res("AppSecondaryBrush")
            });
            box.Children.Add(new TextBlock
            {
                Text = $"{Fmt.Date(cancelled.MoveIn)} to {(cancelled.MoveOut is DateOnly mo ? Fmt.Date(mo) : "present")}",
                Style = (Style)FindResource("MutedText")
            });
            box.Children.Add(new TextBlock
            {
                Text = cancelled.Line,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)FindResource("MutedText")
            });
            HistoryPanel.Children.Add(box);
        }
    }

    private void AddLine(string label, string value, string? tone = null)
    {
        var line = new TextBlock { Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
        line.Inlines.Add(new Run(label + "   ") { Foreground = Res("AppSecondaryBrush") });
        line.Inlines.Add(new Run(value)
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = tone == null ? Res("AppTextBrush") : Res(tone + "Brush")
        });
        InfoPanel.Children.Add(line);
    }


    // ---------- Buttons: each one calls the same action as before ----------

    private void ViewAccount_Click(object sender, RoutedEventArgs e) => _page.ViewAccount_Click(sender, e);
    private void EditTenant_Click(object sender, RoutedEventArgs e) => _page.EditTenant_Click(sender, e);
    private void StayHistory_Click(object sender, RoutedEventArgs e) => _page.StayHistory_Click(sender, e);
    private void MoveIn_Click(object sender, RoutedEventArgs e) => _page.MoveIn_Click(sender, e);
    private void MoveOut_Click(object sender, RoutedEventArgs e) => _page.MoveOut_Click(sender, e);
    private void EditStay_Click(object sender, RoutedEventArgs e) => _page.EditStay_Click(sender, e);
    private void ApplyDeposit_Click(object sender, RoutedEventArgs e) => _page.ApplyDeposit_Click(sender, e);
    private void RefundDeposit_Click(object sender, RoutedEventArgs e) => _page.RefundDeposit_Click(sender, e);
    private void EditDeposit_Click(object sender, RoutedEventArgs e) => _page.EditDeposit_Click(sender, e);
    private void UndoRefund_Click(object sender, RoutedEventArgs e) => _page.UndoRefund_Click(sender, e);
}
