using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Nexabode.Views;

// Makes sure no dialog is taller or wider than the screen, and that a dialog that is too tall can be scrolled.
public static class WindowFit
{
    // Call once when the app starts.
    public static void Register() =>
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || window is Wpf.Ui.Controls.FluentWindow) return;   // the main and sign-in windows handle themselves

        var area = SystemParameters.WorkArea;
        if (window.MaxHeight > area.Height) window.MaxHeight = area.Height;
        if (window.MaxWidth > area.Width) window.MaxWidth = area.Width;

        // A dialog that sizes itself to its content gets a scroll area, so a tall one can't run off the screen.
        if (window.SizeToContent is SizeToContent.Height or SizeToContent.WidthAndHeight
            && window.Content is UIElement content and not ScrollViewer)
        {
            window.Content = null;
            window.Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
                Content = content
            };
        }

        // A window with a fixed size can't be bigger than the screen either.
        if (window.SizeToContent == SizeToContent.Manual)
        {
            if (window.Height > area.Height) window.Height = area.Height;
            if (window.Width > area.Width) window.Width = area.Width;
        }

        // Once it has settled, pull it back on-screen if it ended up hanging over an edge.
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (!window.IsLoaded) return;
            if (window.Top + window.ActualHeight > area.Bottom) window.Top = Math.Max(area.Top, area.Bottom - window.ActualHeight);
            if (window.Left + window.ActualWidth > area.Right) window.Left = Math.Max(area.Left, area.Right - window.ActualWidth);
            if (window.Top < area.Top) window.Top = area.Top;
            if (window.Left < area.Left) window.Left = area.Left;
        }));
    }
}
