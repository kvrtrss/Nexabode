using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Nexabode.Views;

// The print window. The page is shown on the left and the printer, copies and orientation are on the right, so the
// preview and the printing choices are in one place. (Windows' own print window cannot show a preview for this kind
// of document, and its preview area cannot be filled by an app, so Nexabode has its own.)
public partial class PrintPreviewWindow : Window
{
    // A4 at 96 dots per inch, used when a printer does not report its paper size.
    private const double A4Width = 793.7;
    private const double A4Height = 1122.5;

    private readonly string _title;
    private readonly string _what;
    private readonly string? _failureNote;
    private readonly Func<double, double, FlowDocument> _build;
    private LocalPrintServer? _server;
    private bool _ready;   // false while the window is being filled in, so that is not counted as a change

    public bool Printed { get; private set; }

    public PrintPreviewWindow(string title, string what, string? failureNote,
                              Func<double, double, FlowDocument> build)
    {
        InitializeComponent();
        _title = title;
        _what = what;
        _failureNote = failureNote;
        _build = build;

        Title = "Print: " + title;
        TitleText.Text = title;

        OrientationCombo.SelectedIndex = 0;
        LoadPrinters();
        _ready = true;
        RefreshPreview();   // throws if the document cannot be built; PrintPreview.Show reports that

        Closed += (_, _) => _server?.Dispose();
    }

    // ---------- Printers ----------

    private void LoadPrinters()
    {
        try
        {
            _server = new LocalPrintServer();
            var queues = _server
                .GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                .OrderBy(q => q.FullName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            PrinterCombo.ItemsSource = queues;

            string? defaultName = null;
            try { defaultName = LocalPrintServer.GetDefaultPrintQueue().FullName; }
            catch { /* no default printer set */ }

            PrinterCombo.SelectedItem = queues.FirstOrDefault(q => q.FullName == defaultName) ?? queues.FirstOrDefault();
        }
        catch
        {
            PrinterCombo.ItemsSource = null;
        }

        if (PrinterCombo.SelectedItem == null)
        {
            PrinterNote.Text = "No printer was found. Install a printer, or turn on Microsoft Print to PDF in Windows, then open this window again.";
            PrintButton.IsEnabled = false;
        }
    }

    private PrintQueue? SelectedQueue => PrinterCombo.SelectedItem as PrintQueue;
    private bool Landscape => OrientationCombo.SelectedIndex == 1;

    // The printer's own settings with the copies and orientation chosen here.
    private PrintTicket MakeTicket(PrintQueue queue, int copies)
    {
        PrintTicket ticket;
        try { ticket = (queue.UserPrintTicket ?? queue.DefaultPrintTicket).Clone(); }
        catch { ticket = new PrintTicket(); }

        ticket.CopyCount = copies;
        ticket.PageOrientation = Landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        return ticket;
    }

    // A print dialog object used only for printing and for asking the printer how big its page is (it is never shown).
    private PrintDialog MakeDialog(PrintQueue? queue, int copies)
    {
        var dialog = new PrintDialog();
        if (queue == null) return dialog;

        try
        {
            dialog.PrintQueue = queue;
            dialog.PrintTicket = MakeTicket(queue, copies);
        }
        catch
        {
            // The printer would not take the settings; the page size falls back to A4 and its own settings are used.
        }
        return dialog;
    }

    // The printable page of the chosen printer, turned to match the orientation.
    private (double Width, double Height) PageSize(PrintDialog dialog)
    {
        double w = A4Width, h = A4Height;
        try
        {
            var pw = dialog.PrintableAreaWidth;
            var ph = dialog.PrintableAreaHeight;
            if (pw > 100 && ph > 100 && !double.IsInfinity(pw) && !double.IsInfinity(ph)) { w = pw; h = ph; }
        }
        catch { /* keep A4 */ }

        if (Landscape && w < h) (w, h) = (h, w);
        if (!Landscape && w > h) (w, h) = (h, w);
        return (w, h);
    }

    // ---------- Preview ----------

    private void Settings_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Fail($"The {_what} could not be prepared. Details: {ex.Message}");
        }
    }

    // Draws the page for the chosen printer and orientation.
    private void RefreshPreview()
    {
        var dialog = MakeDialog(SelectedQueue, 1);
        var (w, h) = PageSize(dialog);
        var document = _build(w, h);

        // Paper is white with black text, whatever theme the app is in.
        document.Background = Brushes.White;
        document.Foreground = Brushes.Black;
        Reader.Document = document;

        PaperText.Text = $"Paper: {Math.Round(w / 96 * 25.4)} x {Math.Round(h / 96 * 25.4)} mm (the printable area of the chosen printer).";

        // Counted on a separate copy, so the one on screen is left alone.
        try
        {
            var pages = ((IDocumentPaginatorSource)_build(w, h)).DocumentPaginator;
            pages.ComputePageCount();
            PagesText.Text = pages.PageCount == 1 ? "1 page" : $"{pages.PageCount} pages";
        }
        catch
        {
            PagesText.Text = "";
        }
    }

    // ---------- Printing ----------

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var queue = SelectedQueue;
        if (queue == null) { Fail("Choose a printer."); return; }
        if (!int.TryParse(CopiesBox.Text.Trim(), out var copies) || copies < 1 || copies > 99)
        {
            Fail("Enter the number of copies as a whole number from 1 to 99.");
            return;
        }

        try
        {
            var dialog = MakeDialog(queue, copies);
            var (w, h) = PageSize(dialog);

            // A fresh copy sized for the printer's paper (a document can only be shown or printed once).
            var document = _build(w, h);
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, _title);
        }
        catch (Exception ex)   // a printer can fail in many ways; whatever happens, say so and stay on this window
        {
            var message = $"The {_what} could not be printed. Details: {ex.Message}";
            if (_failureNote != null) message += "\n\n" + _failureNote;
            Fail(message);
            return;
        }

        Printed = true;
        DialogResult = true;
    }
}
