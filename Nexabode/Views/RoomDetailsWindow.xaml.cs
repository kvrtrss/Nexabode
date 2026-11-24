using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexabode.Services;

namespace Nexabode.Views;

// Everything about one room, in its own window: its beds, the room and bed actions, and who is staying there.
// It used to be a permanent panel beside the room cards; as a window, the cards keep the whole page width.
// The actions themselves still live on the Rooms page and are only called from here.
public partial class RoomDetailsWindow : Window
{
    private readonly RoomsPage _page;
    private readonly int _roomId;

    public RoomDetailsWindow(RoomsPage page, int roomId)
    {
        InitializeComponent();
        _page = page;
        _roomId = roomId;
        Loaded += (_, _) => Refresh();
    }

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private RoomsPage.RoomInfo? CurrentRoom => _page.RoomFor(_roomId);
    private RoomsPage.BedInfo? SelectedBed => BedGrid.SelectedItem as RoomsPage.BedInfo;

    // Fills the window from the room's current data. Called when it opens and after every action.
    public void Refresh()
    {
        var room = CurrentRoom;
        if (room == null)
        {
            Close();
            return;
        }

        DetailTitle.Text = room.IsActive ? room.Name : $"{room.Name} (Inactive)";

        var parts = new List<string>();
        parts.Add(FloorRules.Label(room.Floor));
        parts.Add(Fmt.Count(room.Beds.Count, "bed"));
        if (!string.IsNullOrWhiteSpace(room.Notes)) parts.Add(room.Notes!);
        DetailSub.Text = string.Join("  -  ", parts);

        RoomToggleButton.Content = room.IsActive ? "Deactivate room" : "Activate room";

        ShowClashBanner(room);

        var keep = _page.BedId;   // setting the list clears the selection, so remember it first
        BedGrid.ItemsSource = room.Beds;
        BedGrid.SelectedItem = room.Beds.FirstOrDefault(b => b.Id == keep);
        _page.BedId = keep;

        UpdateBedButtons();
        RenderOccupants();
    }

    // A red banner when two stays share a bed on the same days: it names the beds and the tenants, and says
    // what to do about it, so whoever manages the data does not have to work it out from the small red lines.
    private void ShowClashBanner(RoomsPage.RoomInfo room)
    {
        var clashing = room.Beds.Where(b => b.HasOverlap).ToList();
        ClashBanner.Visibility = clashing.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (clashing.Count == 0) return;

        ClashTitle.Text = ClashWarnings.RoomTitle(clashing.Count);
        ClashLines.Text = string.Join("\n", clashing.Select(b => ClashWarnings.BedLine(
            b.Label,
            b.Occupants.Where(o => o.OverlapNote != "").Select(o => o.Name).Distinct().ToList())));
        ClashAdvice.Text = ClashWarnings.RoomAdvice(Session.IsOwner);
    }

    // Under each "Overlaps with..." line: fix the stay, or remove it when it was entered by mistake. These are the
    // same actions, with the same safety rules, as Edit this stay and Remove this stay on the Tenants page.
    private StackPanel MakeClashButtons(RoomsPage.OccupantInfo occupant)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

        var edit = new Wpf.Ui.Controls.Button
        {
            Content = "Edit this stay",
            ToolTip = $"Correct the dates or the bed of {occupant.Name}'s stay"
        };
        edit.Click += (_, _) => StayActions.Edit(this, occupant.StayId, () => _page.Reload());
        row.Children.Add(edit);

        // Only an Owner can remove a stay, and only one that was never billed.
        if (Session.IsOwner)
        {
            var remove = new Wpf.Ui.Controls.Button
            {
                Content = "Remove this stay",
                Style = (Style)Application.Current.FindResource("DangerButton"),
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = "For a stay entered by mistake. Deleted if it was never billed, otherwise kept as Cancelled."
            };
            remove.Click += (_, _) => StayActions.Remove(this, occupant.StayId, () => _page.Reload());
            row.Children.Add(remove);
        }

        return row;
    }

    private void OpenTenants_Click(object sender, RoutedEventArgs e)
    {
        var main = Application.Current.MainWindow as MainWindow;
        Close();
        main?.NavigateTo(typeof(TenantsPage));
    }

    // Shows who is staying in the selected bed, or in the whole room when no bed is selected.
    private void RenderOccupants()
    {
        if (OccupantPanel == null) return;   // the page is still being built

        var room = CurrentRoom;
        if (room == null) return;

        var bed = room.Beds.FirstOrDefault(b => b.Id == _page.BedId);

        List<RoomsPage.OccupantInfo> shown;
        if (bed != null)
        {
            OccupantTitle.Text = $"Staying in {bed.Label}";
            shown = bed.Occupants;
        }
        else
        {
            OccupantTitle.Text = $"Staying in {room.Name}";
            shown = room.Beds.SelectMany(b => b.Occupants).ToList();
        }

        OccupantPanel.Children.Clear();

        if (shown.Count == 0)
        {
            OccupantPanel.Children.Add(new TextBlock
            {
                Text = bed != null
                    ? "No one is staying in this bed right now."
                    : "No one is staying in this room right now.",
                Style = (Style)FindResource("MutedText"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var occupant in shown)
        {
            // With a bed selected the bed is already known, so only the dates are shown.
            var detail = bed != null ? "" : occupant.BedLabel + "  -  ";
            detail += occupant.IsReserved
                ? $"Reserved, moves in {Fmt.Date(occupant.MoveIn)}"
                : $"Since {Fmt.Date(occupant.MoveIn)}";
            if (occupant.MoveOut is DateOnly leaving) detail += $", until {Fmt.Date(leaving)}";
            if (occupant.Gender != null) detail += $"  -  {occupant.Gender}";

            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            stack.Children.Add(new TextBlock
            {
                Text = occupant.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("AppTextBrush")
            });
            stack.Children.Add(new TextBlock
            {
                Text = detail,
                Style = (Style)FindResource("MutedText"),
                FontSize = 13
            });
            if (occupant.OverlapNote != "")
            {
                stack.Children.Add(new TextBlock
                {
                    Text = occupant.OverlapNote,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Res("DangerBrush")
                });
                stack.Children.Add(MakeClashButtons(occupant));
            }
            OccupantPanel.Children.Add(stack);
        }
    }

    private void BedGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _page.BedId = SelectedBed?.Id;
        UpdateBedButtons();
        RenderOccupants();
    }

    // The bed actions only show when a bed is selected, and only the ones that apply to that bed.
    private void UpdateBedButtons()
    {
        var room = CurrentRoom;
        var bed = SelectedBed;
        var roomOn = room?.IsActive == true;

        AddBedButton.IsEnabled = roomOn;

        SelectedBedPanel.Visibility = bed != null ? Visibility.Visible : Visibility.Collapsed;
        BedHint.Visibility = bed == null ? Visibility.Visible : Visibility.Collapsed;
        if (bed == null) return;

        var inMaintenance = bed.Status == "Maintenance";

        DeactivateBedButton.Visibility = bed.IsActive ? Visibility.Visible : Visibility.Collapsed;
        ActivateBedButton.Visibility = bed.IsActive ? Visibility.Collapsed : Visibility.Visible;
        StartMaintButton.Visibility = bed.IsActive && !inMaintenance ? Visibility.Visible : Visibility.Collapsed;
        EndMaintButton.Visibility = inMaintenance ? Visibility.Visible : Visibility.Collapsed;

        DeactivateBedButton.IsEnabled = roomOn;
        ActivateBedButton.IsEnabled = roomOn;
        StartMaintButton.IsEnabled = bed.Status == "Available";   // a bed with someone in it cannot go into maintenance
    }


    // ---------- Buttons: each one calls the same action as before ----------

    private void AddBed_Click(object sender, RoutedEventArgs e) => _page.AddBed_Click(sender, e);
    private void EditRoom_Click(object sender, RoutedEventArgs e) => _page.EditRoom_Click(sender, e);
    private void ToggleRoom_Click(object sender, RoutedEventArgs e) => _page.ToggleRoom_Click(sender, e);
    private void EditBed_Click(object sender, RoutedEventArgs e) => _page.EditBed_Click(sender, e);
    private void DeactivateBed_Click(object sender, RoutedEventArgs e) => _page.DeactivateBed_Click(sender, e);
    private void ActivateBed_Click(object sender, RoutedEventArgs e) => _page.ActivateBed_Click(sender, e);
    private void StartMaintenance_Click(object sender, RoutedEventArgs e) => _page.StartMaintenance_Click(sender, e);
    private void EndMaintenance_Click(object sender, RoutedEventArgs e) => _page.EndMaintenance_Click(sender, e);
}
