using System.Windows;

namespace Nexabode.Views;

// The one place that shows a message. Every page used to carry its own copy of a "Tell" helper.
public static class Say
{
    // An explanation, or the reason something could not be done.
    public static void Info(string message, Window? owner = null) => Show(null, message, owner);

    // Something failed. Same window, with a heading so it is not mistaken for a hint.
    public static void Error(string message, Window? owner = null) => Show("Something went wrong", message, owner);

    private static void Show(string? heading, string message, Window? owner)
    {
        // Without an owner, use whichever Nexabode window the person is working in.
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

        var dialog = new MessageDialog(heading, message);
        if (owner is { IsLoaded: true }) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
    }
}
