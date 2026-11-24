using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Nexabode.Views;

// A right-click menu for a table that offers the same actions as the buttons above it, so nothing new has to be
// kept in step: an action shows only when its button is switched on for the selected row, and runs the same code.
public static class GridMenu
{
    public static void Attach(DataGrid grid, params FrameworkElement[] actions)
    {
        var menu = new ContextMenu();
        grid.ContextMenu = menu;

        // A right-click selects the row under the pointer first, as people expect.
        grid.PreviewMouseRightButtonDown += (_, e) =>
        {
            var row = FindRow(e.OriginalSource as DependencyObject);
            if (row == null) return;
            row.IsSelected = true;
            row.Focus();
        };

        grid.ContextMenuOpening += (_, e) =>
        {
            menu.Items.Clear();
            foreach (var action in actions)
            {
                if (!action.IsEnabled) continue;

                var header = action switch
                {
                    ContentControl button => button.Content?.ToString(),
                    MenuItem entry => entry.Header?.ToString(),
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(header)) continue;

                var source = action;
                var item = new MenuItem { Header = header };
                item.Click += (_, _) => source.RaiseEvent(new RoutedEventArgs(
                    source is MenuItem ? MenuItem.ClickEvent : ButtonBase.ClickEvent));
                menu.Items.Add(item);
            }

            if (menu.Items.Count == 0) e.Handled = true;   // nothing to offer, so no empty menu
        };
    }

    private static DataGridRow? FindRow(DependencyObject? from)
    {
        while (from != null && from is not DataGridRow)
            from = VisualTreeHelper.GetParent(from);
        return from as DataGridRow;
    }
}
