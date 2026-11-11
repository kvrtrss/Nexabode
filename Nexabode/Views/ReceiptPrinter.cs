using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Services;

namespace Nexabode.Views;

// Prints a receipt for one recorded payment. It goes straight to the printer: nothing is saved as a file.
// Each printing is written to the change history, so a reprint is marked as a duplicate and can be traced.
public static class ReceiptPrinter
{
    private const string AuditField = "Receipt printed";

    // Returns true when the receipt was sent to the printer.
    public static bool Print(Window? owner, int paymentId)
    {
        ReceiptData? data;
        string? problem;
        try
        {
            (data, problem) = Load(paymentId);
        }
        catch (Exception ex)
        {
            Warn(owner, "The receipt could not be prepared. Details: " + ex.Message);
            return false;
        }

        if (data == null)
        {
            Warn(owner, problem ?? "The receipt could not be prepared.");
            return false;
        }

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return false;

        try
        {
            var document = Build(data, DateTime.Now, printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,
                                      "Receipt " + data.ReceiptNo);
        }
        catch (Exception ex)   // a printer can fail in many ways; whatever happens, say so
        {
            Warn(owner, "The receipt could not be printed. Details: " + ex.Message);
            return false;
        }

        RecordPrint(paymentId, data);
        return true;
    }

    // ---------- Gathering the data ----------

    private static (ReceiptData? Data, string? Problem) Load(int paymentId)
    {
        using var db = new AppDbContext();

        var payment = db.Payments
            .Include(p => p.Tenant)
            .Include(p => p.Allocations).ThenInclude(a => a.Charge).ThenInclude(c => c.UtilityBill)
            .Include(p => p.Allocations).ThenInclude(a => a.Charge).ThenInclude(c => c.Occupancy)
                .ThenInclude(o => o.Bed).ThenInclude(b => b.Room)
            .FirstOrDefault(p => p.Id == paymentId);

        if (payment == null) return (null, "That payment could not be found.");
        if (!ReceiptWording.CanPrint(payment.IsVoided)) return (null, ReceiptWording.VoidedMessage);

        var lines = payment.Allocations
            .OrderBy(a => a.Charge.BillingMonth).ThenBy(a => a.Charge.ChargeType).ThenBy(a => a.ChargeId)
            .Select(a => new ReceiptLine(
                ReceiptWording.ChargeLabel(BillingHelpers.ItemOf(a.Charge), a.Charge.BillingMonth,
                                        a.Charge.AdjustsChargeId != null),
                a.AmountApplied))
            .ToList();
        if (lines.Count == 0) lines.Add(new ReceiptLine(ReceiptWording.NoChargeLine, payment.Amount));

        var places = payment.Allocations
            .Select(a => a.Charge.Occupancy?.Bed)
            .Where(b => b?.Room != null)
            .Select(b => ReceiptWording.PlaceText(b!.Room.RoomName, b.BedLabel))
            .Distinct()
            .ToList();

        // The balance is worked out the same way as the "All balances" tab, as of today.
        var balance = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy)
            .Where(c => c.Occupancy.TenantId == payment.TenantId)
            .ToList()
            .Sum(c => Math.Max(0, c.Amount - c.Allocations.Sum(a => a.AmountApplied)));

        var printedBefore = db.AuditLog.Count(a =>
            a.EntityType == "Payment" && a.EntityId == payment.Id && a.Field == AuditField);

        var settings = AppSettings.Load();
        var data = new ReceiptData(
            ReceiptWording.HouseName(settings.HouseName),
            string.IsNullOrWhiteSpace(settings.HouseAddress) ? null : settings.HouseAddress.Trim(),
            payment.ReceiptNo ?? "",
            payment.PaymentDate,
            payment.Tenant.FullName,
            places.Count == 0 ? null : string.Join("; ", places),
            ReceiptWording.MethodText(payment.Method),
            string.IsNullOrWhiteSpace(payment.ReferenceNo) ? null : payment.ReferenceNo.Trim(),
            string.IsNullOrWhiteSpace(payment.Remarks) ? null : payment.Remarks.Trim(),
            lines,
            payment.Amount,
            payment.AmountReceived ?? payment.Amount,
            balance,
            DateOnly.FromDateTime(DateTime.Today),
            printedBefore + 1);

        return (data, null);
    }

    // The receipt was already printed, so a failure to write the history must not be reported as a failed print.
    private static void RecordPrint(int paymentId, ReceiptData data)
    {
        try
        {
            using var db = new AppDbContext();
            var tenantId = db.Payments.Where(p => p.Id == paymentId).Select(p => (int?)p.TenantId).FirstOrDefault();
            AuditService.Record(db, "Payment", paymentId, tenantId, AuditField, null, data.ReceiptNo,
                                data.PrintNumber > 1 ? $"Duplicate copy, print no. {data.PrintNumber}" : "Original printed");
            db.SaveChanges();
        }
        catch { /* not critical */ }
    }

    private static void Warn(Window? owner, string message)
    {
        if (owner != null) MessageBox.Show(owner, message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Warning);
        else MessageBox.Show(message, "Nexabode", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ---------- The paper ----------

    // Always black on white, whatever theme the app is in, so it prints the same on any printer.
    private static FlowDocument Build(ReceiptData d, DateTime printedAt, double width, double height)
    {
        // The tables below get exact column widths worked out from the paper, so they can never be wider than
        // the page and no column can collapse. A little is taken off so rounding cannot push it over.
        const double padding = 56;
        var contentWidth = Math.Max(300, width - 2 * padding) - 4;

        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            PageWidth = width,
            PageHeight = height,
            PagePadding = new Thickness(padding),
            ColumnWidth = contentWidth,        // one column exactly as wide as the writing area
            IsColumnWidthFlexible = false,
            ColumnGap = 0
        };

        document.Blocks.Add(Centered(d.HouseName, 20, bold: true, bottom: 0));
        if (d.HouseAddress != null) document.Blocks.Add(Centered(d.HouseAddress, 11, dim: true, bottom: 0));

        document.Blocks.Add(Centered(ReceiptWording.Heading, 15, bold: true, top: 16, bottom: 2));
        document.Blocks.Add(Centered("Receipt no. " + d.ReceiptNo, 14, bold: true, bottom: 4));

        if (ReceiptWording.DuplicateLabel(d.PrintNumber) is string duplicate)
            document.Blocks.Add(new Paragraph(new Run(duplicate))
            {
                FontSize = 12, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center,
                BorderBrush = Brushes.Black, BorderThickness = new Thickness(1.5),
                Padding = new Thickness(0, 4, 0, 4), Margin = new Thickness(120, 4, 120, 4)
            });

        // Who, when and how.
        var info = NewTable(contentWidth, 130, fixedFirst: true);
        var infoRows = new TableRowGroup();
        AddInfo(infoRows, "Date", Fmt.Date(d.PaymentDate));
        AddInfo(infoRows, "Received from", d.Tenant);
        if (d.Place != null) AddInfo(infoRows, "Room / bed", d.Place);
        AddInfo(infoRows, "Payment method", d.Method);
        if (d.Reference != null) AddInfo(infoRows, "Reference no.", d.Reference);
        if (d.Remarks != null) AddInfo(infoRows, "Remarks", d.Remarks);
        info.RowGroups.Add(infoRows);
        info.Margin = new Thickness(0, 14, 0, 14);
        document.Blocks.Add(info);

        // What it paid for.
        var lines = NewTable(contentWidth, 130, fixedFirst: false);
        var lineRows = new TableRowGroup();
        AddAmountRow(lineRows, "Paid for", "Amount", bold: true, bottomBorder: true);
        foreach (var line in d.Lines) AddAmountRow(lineRows, line.Description, Fmt.Peso(line.Amount));
        AddAmountRow(lineRows, "Total applied to charges", Fmt.Peso(d.Applied), bold: true, topBorder: true);
        AddAmountRow(lineRows, "Amount received", Fmt.Peso(d.Received));
        var change = ReceiptWording.Change(d.Received, d.Applied);
        if (change > 0) AddAmountRow(lineRows, "Change returned", Fmt.Peso(change));
        lines.RowGroups.Add(lineRows);
        document.Blocks.Add(lines);

        document.Blocks.Add(new Paragraph(new Run(ReceiptWording.BalanceText(d.BalanceRemaining, d.BalanceAsOf)))
            { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 28) });

        document.Blocks.Add(new Paragraph(new Run("Received by: ____________________________________"))
            { Margin = new Thickness(0, 0, 0, 20) });

        foreach (var note in ReceiptWording.Notes)
            document.Blocks.Add(new Paragraph(new Run(note))
                { FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 2) });

        document.Blocks.Add(new Paragraph(new Run(
                $"Printed {printedAt.ToString("MMMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)} by {Session.Administrator}"))
            { FontSize = 10, Foreground = Brushes.DimGray, Margin = new Thickness(0, 10, 0, 0) });

        return document;
    }

    private static Paragraph Centered(string text, double size, bool bold = false, bool dim = false,
                                      double top = 0, double bottom = 4) => new(new Run(text))
    {
        FontSize = size,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Foreground = dim ? Brushes.DimGray : Brushes.Black,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, top, 0, bottom)
    };

    // A two-column table with exact widths: one column of 'fixedWidth', the other taking the rest of the line.
    private static Table NewTable(double contentWidth, double fixedWidth, bool fixedFirst)
    {
        var rest = Math.Max(100, contentWidth - fixedWidth);
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(fixedFirst ? fixedWidth : rest) });
        table.Columns.Add(new TableColumn { Width = new GridLength(fixedFirst ? rest : fixedWidth) });
        return table;
    }

    private static void AddInfo(TableRowGroup group, string label, string value)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run(label)) { Foreground = Brushes.DimGray, Margin = new Thickness(0, 1, 0, 1) }));
        row.Cells.Add(new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(0, 1, 0, 1) }));
        group.Rows.Add(row);
    }

    private static void AddAmountRow(TableRowGroup group, string left, string right, bool bold = false,
                                     bool topBorder = false, bool bottomBorder = false)
    {
        var weight = bold ? FontWeights.Bold : FontWeights.Normal;
        var border = new Thickness(0, topBorder ? 1 : 0, 0, bottomBorder ? 1 : 0);

        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run(left)) { FontWeight = weight, Margin = new Thickness(0, 3, 0, 3) })
            { BorderBrush = Brushes.Black, BorderThickness = border });
        row.Cells.Add(new TableCell(new Paragraph(new Run(right))
            { FontWeight = weight, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 3, 0, 3) })
            { BorderBrush = Brushes.Black, BorderThickness = border });
        group.Rows.Add(row);
    }
}
