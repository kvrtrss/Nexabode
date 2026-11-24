using System.Windows;
using System.Windows.Documents;

namespace Nexabode.Views;

// One way to print anything: one window with the page on the left and the printer choices on the right.
public static class PrintPreview
{
    // 'build' makes the document for a page of the given size. 'what' is for messages ("receipt", "sheet", "report").
    // Returns true when it was sent to the printer.
    public static bool Show(Window? owner, string title, string what, Func<double, double, FlowDocument> build,
                            string? failureNote = null)
    {
        PrintPreviewWindow window;
        try
        {
            window = new PrintPreviewWindow(title, what, failureNote, build);
        }
        catch (Exception ex)
        {
            Say.Error($"The {what} could not be prepared. Details: {ex.Message}", owner);
            return false;
        }

        if (owner is { IsLoaded: true }) window.Owner = owner;
        return window.ShowDialog() == true;
    }
}
