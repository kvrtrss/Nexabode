using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Nexabode.Views;

// Keeps every scroll area independent: the mouse wheel (or trackpad) only moves the one area under the pointer.
//
// WPF already does this by itself. The scroll area closest to the pointer takes the wheel, so a table
// scrolls its own rows and leaves the page and the other panels alone, even at its first or last row.
// The only thing it gets wrong is a scroll area that has nothing to scroll, such as a table with three rows,
// which still swallows the wheel and leaves the page stuck.
//
// So this does nothing at all unless the closest scroll area has nothing to scroll. Then, and only then, the
// wheel goes to the next scroll area outside it that does (usually the page). The wheel is never taken from
// an area that can scroll, and it is never passed on from one at its end. Scrolling itself is left to WPF,
// so a mouse wheel, a trackpad and smooth scrolling all keep working as they do everywhere else.
public static class ScrollForwarding
{
    // Call once when the app starts. It covers every window, including dialogs.
    public static void Register() =>
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel));

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.OriginalSource is not DependencyObject source) return;

        // Drop-down lists live in their own popup window; leave them alone.
        if (PresentationSource.FromDependencyObject(source)?.RootVisual is not Window) return;

        // The scroll area WPF would give the wheel to: the closest one around whatever is under the pointer.
        var closest = ScrollAreaAround(source);
        if (closest == null || HasRoomToScroll(closest)) return;   // it can scroll, or there is none: leave it

        // Nothing to scroll there. Hand the wheel to the closest area outside it that can scroll.
        for (var outer = ScrollAreaAround(Parent(closest)); outer != null; outer = ScrollAreaAround(Parent(outer)))
        {
            if (!HasRoomToScroll(outer)) continue;

            e.Handled = true;
            outer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = outer
            });
            return;
        }
    }

    private static bool HasRoomToScroll(ScrollViewer viewer) =>
        viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && viewer.ScrollableHeight > 0;

    // The closest scroll area that holds this element, or is the element itself.
    private static ScrollViewer? ScrollAreaAround(DependencyObject? element)
    {
        for (var node = element; node != null; node = Parent(node))
            if (node is ScrollViewer viewer) return viewer;
        return null;
    }

    private static DependencyObject? Parent(DependencyObject node) =>
        node is Visual or Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}
