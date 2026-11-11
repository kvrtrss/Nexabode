using System.Globalization;

namespace Nexabode.Services;

// Every payment gets a receipt number automatically, such as RCT-2026-00042: the year of the payment,
// then a running number that starts again at 1 each year. A number is never reused, even when a payment
// is voided. No database or screens in here, so it can be tested on its own.
public static class ReceiptNumberRules
{
    public const string Prefix = "RCT";

    public static string YearPrefix(int year) => $"{Prefix}-{year}-";

    public static string Format(int year, int sequence) => $"{YearPrefix(year)}{sequence:D5}";

    // The running number inside a receipt number of the given year, or false if it isn't one.
    public static bool TryGetSequence(string? number, int year, out int sequence)
    {
        sequence = 0;
        var prefix = YearPrefix(year);
        if (number == null || !number.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return int.TryParse(number.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
    }

    // The whole next receipt number for the year, such as RCT-2026-00004.
    public static string NextNumber(IEnumerable<string?> existing, int year) =>
        Format(year, NextSequence(existing, year));

    // The next running number for the year, given the receipt numbers that already exist.
    public static int NextSequence(IEnumerable<string?> existing, int year) =>
        existing.Select(n => TryGetSequence(n, year, out var s) ? s : 0).DefaultIfEmpty(0).Max() + 1;
}
