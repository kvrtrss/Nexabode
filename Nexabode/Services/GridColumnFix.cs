using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Nexabode.Services;

// Sizes the columns of every table from the table's real width, so they always fit and never push each other out.
//
// WPF's own "fill the rest" (star) columns work their width out once, and when a window is rebuilt (as it is when the
// theme changes) they can keep the answer from a first layout at a different size: a column such as Name stayed wide
// and pushed the others out of view. Here the XAML widths are only read as the plan:
//   - a fixed width (Width="120") stays that width,
//   - a star width (Width="1.4*") is a share of whatever is left, and never goes below the column's MinWidth.
// The plan is turned into real pixel widths when the table appears and every time its width changes.
// Columns can still be dragged wider to read a long entry, but a drag only trades width with the neighbouring column
// (never below its MinWidth), so the table keeps its width and nothing is pushed out of view or hidden.
public static class GridColumnFix
{
    private sealed class Plan
    {
        public bool Captured;
        public double Available = -1;
        public bool Queued;
        public bool Hooked;
        public readonly Dictionary<DataGridColumn, double> Shares = new();   // star columns only: the weight from the XAML
        public readonly Dictionary<DataGridColumn, double> Last = new();     // the width each column was given last
        public bool Busy;                                                     // true while this class itself changes widths
    }

    private static readonly DependencyPropertyDescriptor WidthWatcher =
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));

    private static readonly ConditionalWeakTable<DataGrid, Plan> Plans = new();

    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(DataGrid), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(typeof(DataGrid), FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler(OnSizeChanged));
    }

    // Work the widths out again, for example after a column was shown or hidden.
    public static void Refresh(DataGrid grid)
    {
        var plan = Plans.GetOrCreateValue(grid);
        plan.Available = -1;
        Queue(grid);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        Hook(grid);
        Queue(grid);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is DataGrid grid && e.WidthChanged) Queue(grid);
    }

    // When the table's own vertical scroll bar appears or goes away, the room for the columns changes by one bar.
    private static void Hook(DataGrid grid)
    {
        var plan = Plans.GetOrCreateValue(grid);
        if (plan.Hooked) return;
        grid.ApplyTemplate();
        if (grid.Template.FindName("DG_ScrollViewer", grid) is ScrollViewer viewer)
        {
            plan.Hooked = true;
            viewer.ScrollChanged += (_, args) =>
            {
                if (args.ViewportWidthChange != 0 || args.ExtentHeightChange != 0 || args.ViewportHeightChange != 0)
                    Queue(grid);
            };
        }
    }

    private static void Queue(DataGrid grid)
    {
        var plan = Plans.GetOrCreateValue(grid);
        if (plan.Queued) return;
        plan.Queued = true;
        // After the current layout pass, so the table is not changed in the middle of being measured.
        grid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            plan.Queued = false;
            Apply(grid, plan);
        }));
    }

    private static double RoomForColumns(DataGrid grid)
    {
        var room = grid.ActualWidth - grid.BorderThickness.Left - grid.BorderThickness.Right;
        if (grid.Template.FindName("DG_ScrollViewer", grid) is ScrollViewer viewer
            && viewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
            room -= SystemParameters.VerticalScrollBarWidth;
        return room;
    }

    private static void Apply(DataGrid grid, Plan plan)
    {
        if (!grid.IsLoaded || grid.Columns.Count == 0) return;

        var room = RoomForColumns(grid);
        if (room < 50) return;   // not laid out yet; the next size change will try again
        if (plan.Captured && Math.Abs(plan.Available - room) < 0.5) return;

        // Read the plan from the XAML once, before any width is replaced.
        if (!plan.Captured)
        {
            foreach (var column in grid.Columns)
                if (column.Width.IsStar) plan.Shares[column] = Math.Max(column.Width.Value, 0.0001);
            plan.Captured = true;

            // Watch for the user dragging a column edge.
            foreach (var column in grid.Columns.ToList())
            {
                var watched = column;
                WidthWatcher.AddValueChanged(watched, (_, _) => OnUserResized(grid, plan, watched));
            }
        }
        plan.Available = room;

        // Fixed columns take their width first.
        double fixedTotal = 0;
        var stars = new List<DataGridColumn>();
        foreach (var column in grid.Columns)
        {
            if (column.Visibility != Visibility.Visible) continue;
            if (plan.Shares.ContainsKey(column)) stars.Add(column);
            else fixedTotal += column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
        }

        // The rest is shared by weight. A column that would get less than its minimum is held at the minimum,
        // and the others share what is left.
        var left = room - fixedTotal;
        var widths = new Dictionary<DataGridColumn, double>();
        var open = new List<DataGridColumn>(stars);
        while (open.Count > 0)
        {
            var weight = open.Sum(c => plan.Shares[c]);
            var held = open.Where(c => left * plan.Shares[c] / weight < c.MinWidth).ToList();
            if (held.Count == 0)
            {
                foreach (var c in open) widths[c] = left * plan.Shares[c] / weight;
                break;
            }
            foreach (var c in held)
            {
                widths[c] = c.MinWidth;
                left -= c.MinWidth;
                open.Remove(c);
            }
        }

        plan.Busy = true;
        try
        {
            foreach (var (column, width) in widths)
                column.Width = new DataGridLength(Math.Floor(Math.Max(width, column.MinWidth)));

            plan.Last.Clear();
            foreach (var column in grid.Columns)
                plan.Last[column] = WidthOf(column);
        }
        finally
        {
            plan.Busy = false;
        }
    }

    private static double WidthOf(DataGridColumn column) =>
        column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;

    // The user dragged a column edge. The column that gave or took width is the next visible column to the right
    // (or the one to the left when the last column was dragged), so the total stays the same. Neither column can
    // go under its MinWidth; if that stops the drag, the dragged column stops where its neighbour can no longer give.
    private static void OnUserResized(DataGrid grid, Plan plan, DataGridColumn column)
    {
        if (plan.Busy || !plan.Captured || column.Visibility != Visibility.Visible) return;
        if (!plan.Last.TryGetValue(column, out var before)) return;

        var now = WidthOf(column);
        var change = now - before;
        if (Math.Abs(change) < 0.5) return;

        plan.Busy = true;
        try
        {
            var visible = grid.Columns.Where(c => c.Visibility == Visibility.Visible)
                                      .OrderBy(c => c.DisplayIndex).ToList();
            var at = visible.IndexOf(column);
            var neighbour = at + 1 < visible.Count ? visible[at + 1] : at > 0 ? visible[at - 1] : null;
            if (neighbour == null || !plan.Last.TryGetValue(neighbour, out var neighbourBefore))
            {
                column.Width = new DataGridLength(before);
                return;
            }

            var newColumn = Math.Max(column.MinWidth, now);
            var newNeighbour = neighbourBefore - (newColumn - before);
            if (newNeighbour < neighbour.MinWidth)
            {
                newNeighbour = neighbour.MinWidth;
                newColumn = before + (neighbourBefore - newNeighbour);
            }

            column.Width = new DataGridLength(Math.Floor(newColumn));
            neighbour.Width = new DataGridLength(Math.Floor(newNeighbour));
            plan.Last[column] = Math.Floor(newColumn);
            plan.Last[neighbour] = Math.Floor(newNeighbour);
        }
        finally
        {
            plan.Busy = false;
        }
    }
}
