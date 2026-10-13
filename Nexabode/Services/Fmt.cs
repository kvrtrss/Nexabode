using System.Globalization;

namespace Nexabode.Services;

public static class Fmt
{
    public static string Peso(decimal value) =>
        (value < 0 ? "-₱" : "₱") + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);

    public static string Date(DateOnly date) =>
        date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}