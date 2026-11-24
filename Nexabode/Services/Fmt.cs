using System.Globalization;

namespace Nexabode.Services;

public static class Fmt
{
    public static string Peso(decimal value) =>
        (value < 0 ? "-₱" : "₱") + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);

    // "1 bed", "2 beds", "0 beds": a count with the right singular or plural word.
    public static string Count(int number, string singular, string? plural = null) =>
        $"{number} {(number == 1 ? singular : plural ?? singular + "s")}";

    public static string Date(DateOnly date) =>
        date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}