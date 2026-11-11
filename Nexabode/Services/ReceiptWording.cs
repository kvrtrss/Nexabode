using System.Globalization;

namespace Nexabode.Services;

// One line on the receipt: what the money was applied to, and how much of it.
public sealed record ReceiptLine(string Description, decimal Amount);

// Everything printed on a payment receipt, already gathered from the database.
public sealed record ReceiptData(
    string HouseName, string? HouseAddress,
    string ReceiptNo, DateOnly PaymentDate,
    string Tenant, string? Place,
    string Method, string? Reference, string? Remarks,
    IReadOnlyList<ReceiptLine> Lines,
    decimal Applied, decimal Received,
    decimal BalanceRemaining, DateOnly BalanceAsOf,
    int PrintNumber);

// The words and small rules of the printed payment receipt, kept apart from the printing so they can be tested.
// No database or screens in here.
public static class ReceiptWording
{
    public const string Heading = "PAYMENT RECEIPT";

    public const string NoChargeLine = "Payment received";

    public static IReadOnlyList<string> Notes { get; } = new[]
    {
        "Please keep this receipt as your proof of payment.",
        "If a payment was entered by mistake it is voided in Nexabode, and its receipt is then no longer valid."
    };

    public static string HouseName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Boarding House" : name.Trim();

    // A voided payment never happened as far as the books go, so nobody should be handed a receipt for it.
    public static bool CanPrint(bool isVoided) => !isVoided;

    public const string VoidedMessage =
        "This payment was voided, so a receipt cannot be printed for it.";

    // "Rent, October 2026" or "Electricity, October 2026 (adjustment)".
    public static string ChargeLabel(string item, string billingMonth, bool isAdjustment)
    {
        var month = MonthText(billingMonth);
        var label = month.Length > 0 ? $"{item}, {month}" : item;
        return isAdjustment ? label + " (adjustment)" : label;
    }

    // Billing months are stored as "2026-10". Anything else is shown as typed.
    public static string MonthText(string? billingMonth)
    {
        var text = (billingMonth ?? "").Trim();
        return DateTime.TryParseExact(text + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out var month)
            ? month.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : text;
    }

    // The money handed over beyond what was applied to charges is change to give back.
    public static decimal Change(decimal received, decimal applied) => Math.Max(0, received - applied);

    public static string PlaceText(string room, string bed) => $"{room.Trim()}, {bed.Trim()}";

    public static string MethodText(string? method) =>
        string.IsNullOrWhiteSpace(method) ? "Not stated" : method.Trim();

    public static string BalanceText(decimal balance, DateOnly asOf) =>
        balance > 0
            ? $"Balance remaining as of {Fmt.Date(asOf)}: {Fmt.Peso(balance)}"
            : $"No balance remaining as of {Fmt.Date(asOf)}";

    // What the "Payment recorded" window says. Change is only mentioned when there is some to give back.
    public static string PaymentRecordedMessage(string receiptNo, string tenant, decimal received, decimal applied)
    {
        var text = $"Payment recorded (receipt no. {receiptNo}).";
        var change = Change(received, applied);
        if (change > 0)
            text += $"\n\nChange to return to {tenant}: {Fmt.Peso(change)}\n" +
                    $"(received {Fmt.Peso(received)}, applied {Fmt.Peso(applied)})";
        return text;
    }

    // The first printing is the original. Every later one says so, so a reprint cannot pass as a second payment.
    public static string? DuplicateLabel(int printNumber) =>
        printNumber > 1 ? $"DUPLICATE COPY (print no. {printNumber})" : null;
}
