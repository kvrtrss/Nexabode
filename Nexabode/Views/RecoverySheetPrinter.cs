using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Nexabode.Services;

namespace Nexabode.Views;

// Prints a recovery code or an emergency access key on paper, for the person to keep somewhere safe.
// It is shown in a preview and printed from there: nothing is saved as a file by Nexabode, and nothing is put on the clipboard.
public static class RecoverySheetPrinter
{
    // Returns true when the sheet was sent to the printer. 'secret' is the code or key, already formatted.
    public static bool Print(Window? owner, RecoverySheetKind kind, string? account, string secret)
    {
        return PrintPreview.Show(owner, RecoverySheetText.Heading(kind), "sheet",
            (width, height) => Build(kind, account, secret, DateTime.Now, width, height),
            failureNote: "You can still write it down by hand.");
    }

    // Always black on white, whatever theme the app is in, so it prints the same on any printer.
    private static FlowDocument Build(RecoverySheetKind kind, string? account, string secret, DateTime madeOn,
                                      double width, double height)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            PageWidth = width,
            PageHeight = height,
            ColumnWidth = width,
            PagePadding = new Thickness(64)
        };

        document.Blocks.Add(new Paragraph(new Run(RecoverySheetText.Heading(kind)))
            { FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });

        document.Blocks.Add(new Paragraph(new Run(RecoverySheetText.Purpose(kind)))
            { Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 22) });

        document.Blocks.Add(new Paragraph(new Run(RecoverySheetText.SecretCaption(kind)))
            { FontSize = 12, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
              Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 6) });

        document.Blocks.Add(new Paragraph(new Run(secret))
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(0, 20, 0, 20),
            Margin = new Thickness(0, 0, 0, 22)
        });

        var details = new List<string>();
        if (RecoverySheetText.AccountLine(kind, account) is string line) details.Add(line);
        details.Add("Created on: " + madeOn.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture));
        foreach (var text in details)
            document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0, 0, 0, 2) });

        document.Blocks.Add(new Paragraph(new Run("Kept at: ______________________________________________"))
            { Margin = new Thickness(0, 14, 0, 22) });

        document.Blocks.Add(new Paragraph(new Run("How to use it"))
            { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        document.Blocks.Add(NumberedList(RecoverySheetText.Steps(kind), TextMarkerStyle.Decimal));

        document.Blocks.Add(new Paragraph(new Run("Keep it safe"))
            { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 6) });
        document.Blocks.Add(NumberedList(RecoverySheetText.Cautions(kind), TextMarkerStyle.Disc));

        return document;
    }

    private static List NumberedList(IEnumerable<string> items, TextMarkerStyle marker)
    {
        var list = new List { MarkerStyle = marker, Margin = new Thickness(0, 0, 0, 0) };
        foreach (var item in items)
            list.ListItems.Add(new ListItem(new Paragraph(new Run(item)) { Margin = new Thickness(0, 0, 0, 4) }));
        return list;
    }
}
