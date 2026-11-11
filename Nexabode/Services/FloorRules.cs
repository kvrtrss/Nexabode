namespace Nexabode.Services;

// How floors are written, checked, named and ordered. Pure logic, no screen code.
public static class FloorRules
{
    public const int MaxLength = 20;
    public const string NoFloorLabel = "No floor set";

    // Null if the floor is acceptable, otherwise a message for the person.
    public static string? Validate(string? floor)
    {
        var text = Normalize(floor);
        if (text == null) return "Please enter the floor, for example 1, 2 or Ground.";
        if (text.Length > MaxLength) return $"The floor must be {MaxLength} characters or fewer.";
        return null;
    }

    // The one way a floor is stored, so "1", "01", "Floor 1" and "floor  1" all mean the same floor.
    // Returns null for blank input.
    public static string? Normalize(string? floor)
    {
        if (string.IsNullOrWhiteSpace(floor)) return null;

        var text = string.Join(" ", floor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        // Drop a leading "Floor" / "Level" so people can type either way.
        foreach (var prefix in new[] { "floor ", "level " })
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(prefix.Length).Trim();
                break;
            }
        // A trailing "floor" too ("Ground floor").
        if (text.EndsWith(" floor", StringComparison.OrdinalIgnoreCase))
            text = text.Substring(0, text.Length - 6).Trim();

        if (text.Length == 0 || text.Equals("floor", StringComparison.OrdinalIgnoreCase)
                             || text.Equals("level", StringComparison.OrdinalIgnoreCase)) return null;

        switch (text.ToLowerInvariant())
        {
            case "g": case "gf": case "ground": return "Ground";
            case "b": case "basement": return "Basement";
        }

        if (long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n))
            return n.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    // The heading shown above the rooms on that floor.
    public static string Label(string? floor)
    {
        var f = Normalize(floor);
        if (f == null) return NoFloorLabel;
        if (f == "Ground") return "Ground floor";
        if (f == "Basement") return "Basement";
        return f.Any(char.IsDigit) ? "Floor " + f : f + " floor";
    }

    // Orders floors: basement, ground, then 1, 2, ... 10 (by number, not by letters), then anything
    // else A-Z, and rooms with no floor last.
    public static int Compare(string? a, string? b)
    {
        var x = Normalize(a);
        var y = Normalize(b);
        var rx = Rank(x);
        var ry = Rank(y);
        if (rx != ry) return rx.CompareTo(ry);

        if (rx == 2)
        {
            var nx = long.Parse(x!, System.Globalization.CultureInfo.InvariantCulture);
            var ny = long.Parse(y!, System.Globalization.CultureInfo.InvariantCulture);
            return nx.CompareTo(ny);
        }
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }

    private static int Rank(string? f)
    {
        if (f == null) return 4;
        if (f == "Basement") return 0;
        if (f == "Ground") return 1;
        return long.TryParse(f, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _) ? 2 : 3;
    }
}
